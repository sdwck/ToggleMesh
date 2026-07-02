import threading
import json
import re
import logging
import time
import os
import queue
import hashlib
import random
from typing import Dict, Any, Callable, Set, Optional
import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

from .models import ToggleMeshOptions, FeatureFlagDto, SegmentDto, RuleDto
from .rules import RuleEngine, CachedFlag, CachedSegment, evaluate_rollout

logger = logging.getLogger("togglemesh")

def to_snake_case(obj):
    if isinstance(obj, list):
        return [to_snake_case(i) for i in obj]
    elif isinstance(obj, dict):
        return { re.sub(r'(?<!^)(?=[A-Z])', '_', k).lower(): to_snake_case(v) for k, v in obj.items() }
    return obj

class FlagMetrics:
    def __init__(self):
        self.true_count = 0
        self.false_count = 0

class ToggleMeshClient:
    def __init__(self, options: ToggleMeshOptions):
        self.options = options
        self.base_url = options.base_url.rstrip("/")
        
        self._flags_cache: Dict[str, CachedFlag] = {}
        self._segments_cache: Dict[str, CachedSegment] = {}
        self.listeners: Set[Callable[[], None]] = set()
        
        self.rule_engine = RuleEngine(segment_provider=self)
        
        self._polling_thread = None
        self._metrics_thread = None
        self._events_thread = None
        
        self._stop_event = threading.Event()
        self._lock = threading.Lock()
        
        self._metrics_buffer: Dict[str, FlagMetrics] = {}
        self._metrics_lock = threading.Lock()
        self._events_queue = queue.Queue(maxsize=self.options.analytics_channel_capacity)
        
        self._session = self._create_session()
        self._fallback_path = self._resolve_fallback_path()
        
        if self.options.use_fallback_file:
            self._load_fallback()
            
        self._sync_state()
        self._start_threads()

    def _create_session(self) -> requests.Session:
        session = requests.Session()
        session.verify = not self.options.disable_ssl_verification
        retry = Retry(
            total=3,
            read=False,
            backoff_factor=1,
            status_forcelist=[500, 502, 503, 504],
            allowed_methods=["GET", "POST"]
        )
        adapter = HTTPAdapter(max_retries=retry)
        session.mount("http://", adapter)
        session.mount("https://", adapter)
        return session

    def _resolve_fallback_path(self) -> Optional[str]:
        if not self.options.use_fallback_file:
            return None
        if self.options.fallback_file_path:
            return self.options.fallback_file_path
            
        safe_key = hashlib.sha256(self.options.client_key.encode('utf-8')).hexdigest()[:12]
        base_dir = os.path.join(os.getcwd(), ".togglemesh")
        return os.path.join(base_dir, f"{safe_key}.json")

    def get_segment_rules(self, segment_id: str):
        segment = self._segments_cache.get(segment_id)
        return segment.groups if segment else None

    def is_enabled(self, flag_key: str, default_value: bool = False, *, identity: str = None, context: Dict[str, str] = None) -> bool:
        with self._lock:
            flag = self._flags_cache.get(flag_key)
            context = context or {}
            
        if not flag:
            return default_value
            
        active_rollout_percentage = flag.rollout_percentage
        
        if flag.parsed_contextual_rollouts and flag.original_dto.context_partition_keys:
            parts = []
            for key in flag.original_dto.context_partition_keys:
                parts.append(str(context.get(key, "null")))
            slice_key = "|".join(parts)
            if slice_key in flag.parsed_contextual_rollouts:
                active_rollout_percentage = flag.parsed_contextual_rollouts[slice_key]
                
        if not flag.is_enabled or not self.rule_engine.evaluate(flag.groups, context):
            result = False
        else:
            result = evaluate_rollout(active_rollout_percentage, flag_key, identity)

        self._update_metrics(flag_key, result)
        
        if identity and flag.is_experiment_active:
            self._queue_event(
                evt_type=0,
                identity=identity,
                flag_key=flag_key,
                result=result,
                properties=context
            )
            
        return result

    def track(self, event_name: str, properties: Any = None, value: float = None, *, identity: str = None):
        if not identity or not event_name:
            return
            
        self._queue_event(
            evt_type=1,
            identity=identity,
            event_name=event_name,
            properties=properties,
            value=value
        )

    def subscribe(self, listener: Callable[[], None]) -> Callable[[], None]:
        with self._lock:
            self.listeners.add(listener)
        def unsubscribe():
            with self._lock:
                self.listeners.discard(listener)
        return unsubscribe

    def _notify_listeners(self):
        with self._lock:
            callbacks = list(self.listeners)
        for cb in callbacks:
            try:
                cb()
            except Exception as e:
                logger.error(f"[ToggleMesh] Listener callback error: {e}")

    def _update_metrics(self, flag_key: str, result: bool):
        if not self.options.is_metrics_enabled: return
        with self._metrics_lock:
            if flag_key not in self._metrics_buffer:
                if len(self._metrics_buffer) >= self.options.metrics_buffer_capacity: return
                self._metrics_buffer[flag_key] = FlagMetrics()
            if result:
                self._metrics_buffer[flag_key].true_count += 1
            else:
                self._metrics_buffer[flag_key].false_count += 1

    def _queue_event(self, evt_type: int, identity: str, flag_key: str = None, 
                     result: bool = False, event_name: str = None, 
                     properties: Any = None, value: float = None):
        if not self.options.is_metrics_enabled: return
        try:
            evt = {
                "Type": evt_type,
                "Timestamp": int(time.time() * 1000),
                "Identity": identity,
                "Properties": properties
            }
            if flag_key: evt["FlagKey"] = flag_key
            if result is not None: evt["Result"] = result
            if event_name: evt["EventName"] = event_name
            if value is not None: evt["Value"] = value
            
            self._events_queue.put_nowait(evt)
        except queue.Full:
            pass

    def _sync_state(self) -> None:
        url = f"{self.base_url}/api/v1/sdk/flags"
        headers = {"x-api-key": self.options.client_key}
        try:
            response = self._session.get(url, headers=headers, timeout=10)
            if response.status_code != 200:
                logger.warning(f"[ToggleMesh] Failed to sync state: {response.status_code}")
                return
            
            data = to_snake_case(response.json())
            
            with self._lock:
                self._flags_cache.clear()
                self._segments_cache.clear()
                
                for f_data in data.get("flags", []):
                    rules = [RuleDto.from_dict(r) for r in f_data.get("rules", [])]
                    f_data["rules"] = rules
                    dto = FeatureFlagDto.from_dict(f_data)
                    self._cache_flag(dto)
                    
                for s_data in data.get("segments", []):
                    rules = [RuleDto.from_dict(r) for r in s_data.get("rules", [])]
                    s_data["rules"] = rules
                    dto = SegmentDto.from_dict(s_data)
                    self._cache_segment(dto)
                    
            self._notify_listeners()
            self._save_fallback(data)
        except Exception as e:
            logger.error(f"[ToggleMesh] Error syncing state: {e}")

    def _cache_flag(self, dto: FeatureFlagDto):
        groups = self.rule_engine.compile_rules(dto.rules)
        self._flags_cache[dto.key] = CachedFlag(dto, groups)
        
    def _cache_segment(self, dto: SegmentDto):
        groups = self.rule_engine.compile_rules(dto.rules)
        self._segments_cache[dto.id] = CachedSegment(dto, groups)

    def _load_fallback(self):
        if not self._fallback_path or not os.path.exists(self._fallback_path):
            return
        try:
            with open(self._fallback_path, 'r', encoding='utf-8') as f:
                data = to_snake_case(json.load(f))
                with self._lock:
                    for f_data in data.get("flags", []):
                        rules = [RuleDto.from_dict(r) for r in f_data.get("rules", [])]
                        f_data["rules"] = rules
                        self._cache_flag(FeatureFlagDto.from_dict(f_data))
                    for s_data in data.get("segments", []):
                        rules = [RuleDto.from_dict(r) for r in s_data.get("rules", [])]
                        s_data["rules"] = rules
                        self._cache_segment(SegmentDto.from_dict(s_data))
                logger.info("[ToggleMesh] Loaded fallback state.")
        except Exception as e:
            logger.error(f"[ToggleMesh] Failed to load fallback: {e}")

    def _save_fallback(self, data: Any):
        if not self._fallback_path: return
        try:
            os.makedirs(os.path.dirname(self._fallback_path), exist_ok=True)
            with open(self._fallback_path + '.tmp', 'w', encoding='utf-8') as f:
                json.dump(data, f)
            os.replace(self._fallback_path + '.tmp', self._fallback_path)
        except Exception as e:
            logger.error(f"[ToggleMesh] Failed to save fallback: {e}")

    def _start_threads(self) -> None:
        self._stop_event.clear()
        self._polling_thread = threading.Thread(target=self._sse_loop, daemon=True)
        self._polling_thread.start()
        
        if self.options.is_metrics_enabled:
            self._metrics_thread = threading.Thread(target=self._metrics_flusher, daemon=True)
            self._metrics_thread.start()
            
            self._events_thread = threading.Thread(target=self._events_flusher, daemon=True)
            self._events_thread.start()

    def _sse_loop(self) -> None:
        url = f"{self.base_url}/api/v1/stream"
        headers = {"x-api-key": self.options.client_key, "Accept": "text/event-stream"}
        backoff = 1
        
        while not self._stop_event.is_set():
            try:
                with self._session.get(url, headers=headers, stream=True, timeout=60) as response:
                    if response.status_code == 401:
                        logger.critical("[ToggleMesh] Invalid API Key. Background sync loop stopped permanently.")
                        self.stop()
                        break
                    
                    if response.status_code != 200:
                        raise Exception(f"Bad status code {response.status_code}")
                        
                    backoff = 1
                    for line in response.iter_lines(decode_unicode=True):
                        if self._stop_event.is_set():
                            break
                        if line and line.startswith("data: "):
                            self._handle_sse_event(line[6:])
            except Exception as e:
                logger.debug(f"[ToggleMesh] SSE connection error: {e}. Reconnecting in {backoff}s...")
            
            if self._stop_event.is_set():
                break
                
            jitter = random.uniform(0.0, 1.0)
            wait_time = backoff + jitter
            self._stop_event.wait(wait_time)
            backoff = min(backoff * 2.0, 30.0)
            
            if not self._stop_event.is_set():
                self._sync_state()

    def _handle_sse_event(self, data: str):
        try:
            doc = json.loads(data)
            event_name = doc.get("EventName")
            if event_name == "FlagUpdated" and "Payload" in doc:
                payload = doc["Payload"]
                if isinstance(payload, str): payload = json.loads(payload)
                
                payload = to_snake_case(payload)
                
                payload["rules"] = [RuleDto.from_dict(r) for r in payload.get("rules", [])]
                dto = FeatureFlagDto.from_dict(payload)
                with self._lock:
                    self._cache_flag(dto)
                self._notify_listeners()
            elif event_name == "StateReloadRequired":
                self._sync_state()
        except Exception as e:
            logger.error(f"[ToggleMesh] Failed to parse SSE event: {e}")

    def _metrics_flusher(self):
        url = f"{self.base_url}/api/v1/sdk/metrics"
        headers = {"x-api-key": self.options.client_key, "Content-Type": "application/json"}
        
        while not self._stop_event.is_set():
            if self._stop_event.wait(10):
                break
                
            payload = []
            with self._metrics_lock:
                for key, m in list(self._metrics_buffer.items()):
                    if m.true_count > 0 or m.false_count > 0:
                        payload.append({"Key": key, "TrueCount": m.true_count, "FalseCount": m.false_count})
                        m.true_count = 0
                        m.false_count = 0
                        
            if not payload: continue
                
            try:
                self._session.post(url, json=payload, headers=headers, timeout=5)
            except Exception as e:
                logger.debug(f"[ToggleMesh] Failed to flush metrics: {e}")
                with self._metrics_lock:
                    for item in payload:
                        if item["Key"] not in self._metrics_buffer:
                            self._metrics_buffer[item["Key"]] = FlagMetrics()
                        self._metrics_buffer[item["Key"]].true_count += item["TrueCount"]
                        self._metrics_buffer[item["Key"]].false_count += item["FalseCount"]

    def _events_flusher(self):
        url = f"{self.base_url}/api/v1/sdk/events"
        headers = {"x-api-key": self.options.client_key, "Content-Type": "application/json"}
        
        while not self._stop_event.is_set():
            if self._stop_event.wait(10):
                break
                
            batch = []
            while not self._events_queue.empty() and len(batch) < self.options.max_batch_size:
                try:
                    batch.append(self._events_queue.get_nowait())
                except queue.Empty:
                    break
                    
            if not batch: continue
                
            try:
                self._session.post(url, json={"Events": batch}, headers=headers, timeout=5)
            except Exception as e:
                logger.debug(f"[ToggleMesh] Failed to flush events: {e}")
                for evt in batch:
                    try:
                        self._events_queue.put_nowait(evt)
                    except queue.Empty:
                        pass
                    except queue.Full:
                        break

    def stop(self):
        self._stop_event.set()
        if self._polling_thread: self._polling_thread.join(timeout=2.0)
        if self._metrics_thread: self._metrics_thread.join(timeout=2.0)
        if self._events_thread: self._events_thread.join(timeout=2.0)
