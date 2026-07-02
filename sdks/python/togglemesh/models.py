from dataclasses import dataclass, field
from typing import List, Dict, Any, Optional
import inspect

@dataclass
class ToggleMeshOptions:
    base_url: str
    client_key: str
    refresh_interval: int = 60
    is_metrics_enabled: bool = True
    use_fallback_file: bool = False
    fallback_file_path: Optional[str] = None
    disable_ssl_verification: bool = False
    analytics_channel_capacity: int = 10000
    metrics_buffer_capacity: int = 10000
    max_batch_size: int = 2000

@dataclass
class FlagState:
    key: str
    is_enabled: bool

def _from_dict(cls, data):
    valid_keys = inspect.signature(cls).parameters.keys()
    filtered = {k: v for k, v in data.items() if k in valid_keys}
    return cls(**filtered)

@dataclass
class RuleDto:
    group_id: int
    attribute: str
    operator: str
    value: str

    @classmethod
    def from_dict(cls, data):
        return _from_dict(cls, data)

@dataclass
class FeatureFlagDto:
    key: str
    is_enabled: bool
    is_experiment_active: bool
    rollout_percentage: Optional[int]
    context_partition_keys: Optional[List[str]]
    contextual_rollouts: Optional[Dict[str, int]]
    rules: List[RuleDto]

    @classmethod
    def from_dict(cls, data):
        return _from_dict(cls, data)

@dataclass
class SegmentDto:
    id: str
    name: str
    rules: List[RuleDto]

    @classmethod
    def from_dict(cls, data):
        return _from_dict(cls, data)

@dataclass
class SdkGetFlagsResponse:
    flags: List[FeatureFlagDto]
    segments: List[SegmentDto]
