from dataclasses import dataclass
from typing import List, Dict, Optional
import inspect
import re

@dataclass
class ToggleMeshOptions:
    base_url: str
    server_key: str
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

def camel_to_snake(name):
    s1 = re.sub('(.)([A-Z][a-z]+)', r'\1_\2', name)
    return re.sub('([a-z0-9])([A-Z])', r'\1_\2', s1).lower()

def _from_dict(cls, data):
    valid_keys = inspect.signature(cls).parameters.keys()
    filtered = {camel_to_snake(k): v for k, v in data.items() if camel_to_snake(k) in valid_keys}
    return cls(**filtered)

@dataclass
class VariationWeight:
    variation_id: str
    weight: int

    @classmethod
    def from_dict(cls, data):
        return _from_dict(cls, data)

@dataclass
class RuleDto:
    priority: int
    group_id: int
    attribute: str
    operator: str
    value: str
    rollout: List[VariationWeight]

    @classmethod
    def from_dict(cls, data):
        return _from_dict(cls, data)

@dataclass
class FeatureFlagDto:
    key: str
    is_enabled: bool
    is_experiment_active: bool
    rules: List[RuleDto]
    off_variation_id: Optional[str]
    fallthrough_rollout: List[VariationWeight]
    variations: Dict[str, str]
    contextual_rollouts: Optional[Dict[str, List[VariationWeight]]]
    context_partition_keys: Optional[List[str]]
    individual_targets: Optional[Dict[str, str]]

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
