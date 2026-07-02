import json
from dataclasses import dataclass
from typing import List, Dict, Any, Optional

from .models import FeatureFlagDto, RuleDto, SegmentDto
from .operators import OPERATOR_MAP, RuleOperator, FalseOperator, InSegmentOperator

def calculate_fnv1a_hash(text: str) -> int:
    offset_basis = 2166136261
    prime = 16777619
    hash_val = offset_basis
    
    encoded = text.encode("utf-8")
    for byte in encoded:
        hash_val ^= byte
        hash_val = (hash_val * prime) & 0xFFFFFFFF
    
    return hash_val

def evaluate_rollout(rollout_percentage: Optional[int], flag_key: str, identity: str) -> bool:
    if rollout_percentage is None:
        return True
    if rollout_percentage <= 0:
        return False
    if rollout_percentage >= 100:
        return True
    if not identity:
        return False
        
    hash_val = calculate_fnv1a_hash(flag_key + identity)
    bucket = hash_val % 100
    return bucket < rollout_percentage


@dataclass
class CompiledRule:
    attribute: str
    operator: RuleOperator
    compiled_value: Any

@dataclass
class CompiledRuleGroup:
    rules: List[CompiledRule]

class CachedFlag:
    def __init__(self, dto: FeatureFlagDto, groups: List[CompiledRuleGroup]):
        self.key = dto.key
        self.is_enabled = dto.is_enabled
        self.rollout_percentage = dto.rollout_percentage
        self.contextual_rollouts = dto.contextual_rollouts
        self.is_experiment_active = dto.is_experiment_active
        self.groups = groups
        self.original_dto = dto
        
        self.parsed_contextual_rollouts = {}
        if dto.contextual_rollouts and dto.context_partition_keys:
            for k, v in dto.contextual_rollouts.items():
                try:
                    d = json.loads(k)
                    parts = []
                    for key in dto.context_partition_keys:
                        parts.append(str(d.get(key, "null")))
                    slice_key = "|".join(parts)
                    self.parsed_contextual_rollouts[slice_key] = v
                except Exception:
                    pass

class CachedSegment:
    def __init__(self, dto: SegmentDto, groups: List[CompiledRuleGroup]):
        self.id = dto.id
        self.name = dto.name
        self.groups = groups


class RuleEngine:
    def __init__(self, segment_provider=None):
        self.segment_provider = segment_provider

    def compile_rules(self, rules: List[RuleDto]) -> List[CompiledRuleGroup]:
        if not rules:
            return []
            
        groups_dict = {}
        for r in rules:
            if r.group_id not in groups_dict:
                groups_dict[r.group_id] = []
            groups_dict[r.group_id].append(r)
            
        compiled_groups = []
        for g_id, g_rules in groups_dict.items():
            compiled_rules = []
            for r in g_rules:
                if r.operator.lower() == "insegment":
                    op = InSegmentOperator()
                else:
                    op = OPERATOR_MAP.get(r.operator.lower(), FalseOperator())
                    
                compiled_rules.append(CompiledRule(
                    attribute=r.attribute,
                    operator=op,
                    compiled_value=op.compile(r.value)
                ))
            compiled_groups.append(CompiledRuleGroup(rules=compiled_rules))
            
        return compiled_groups

    def evaluate(self, groups: List[CompiledRuleGroup], context: Dict[str, str]) -> bool:
        if not groups:
            return True
            
        for group in groups:
            group_passed = True
            for rule in group.rules:
                if isinstance(rule.operator, InSegmentOperator):
                    if self.segment_provider:
                        segment_rules = self.segment_provider.get_segment_rules(rule.compiled_value)
                        if segment_rules is not None and self.evaluate(segment_rules, context):
                            continue
                else:
                    user_value = context.get(rule.attribute)
                    if user_value is not None:
                        if rule.operator.evaluate(str(user_value), rule.compiled_value):
                            continue
                
                group_passed = False
                break
                
            if group_passed:
                return True
                
        return False
