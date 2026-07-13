import json
from dataclasses import dataclass
from typing import List, Dict, Any, Optional

from .models import FeatureFlagDto, RuleDto, SegmentDto, VariationWeight
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

def evaluate_rollout(rollout: List['VariationWeight'], flag_key: str, identity: str) -> Optional[str]:
    if not rollout:
        return None
    if len(rollout) == 1:
        return rollout[0].variation_id
    if not identity:
        return rollout[0].variation_id
        
    hash_val = calculate_fnv1a_hash(flag_key + identity)
    bucket = hash_val % 10000
    
    current_sum = 0
    for w in rollout:
        weight = w.get("weight", 0) if isinstance(w, dict) else w.weight
        variation_id = w.get("variation_id") if isinstance(w, dict) else w.variation_id
        
        current_sum += weight
        if bucket < current_sum:
            return variation_id
            
    last_w = rollout[-1]
    return last_w.get("variation_id") if isinstance(last_w, dict) else last_w.variation_id


@dataclass
class CompiledRule:
    attribute: str
    operator: RuleOperator
    compiled_value: Any

@dataclass
class CompiledRuleGroup:
    priority: int
    rollout: List['VariationWeight']
    rules: List[CompiledRule]

class CachedFlag:
    def __init__(self, dto: FeatureFlagDto, groups: List[CompiledRuleGroup]):
        self.key = dto.key
        self.is_enabled = dto.is_enabled
        self.off_variation_id = dto.off_variation_id
        self.fallthrough_rollout = dto.fallthrough_rollout
        self.variations = dto.variations
        self.individual_targets = dto.individual_targets
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
            compiled_groups.append(CompiledRuleGroup(
                priority=g_rules[0].priority,
                rollout=g_rules[0].rollout,
                rules=compiled_rules
            ))
            
        compiled_groups.sort(key=lambda g: g.priority)
        return compiled_groups

    def evaluate(self, groups: List[CompiledRuleGroup], context: Dict[str, str]) -> Optional[CompiledRuleGroup]:
        if not groups:
            return None
            
        for group in groups:
            group_passed = True
            for rule in group.rules:
                if isinstance(rule.operator, InSegmentOperator):
                    if self.segment_provider:
                        segment_rules = self.segment_provider.get_segment_rules(rule.compiled_value)
                        if segment_rules is not None and self.evaluate(segment_rules, context) is not None:
                            continue
                else:
                    user_value = context.get(rule.attribute)
                    if user_value is not None:
                        if rule.operator.evaluate(str(user_value), rule.compiled_value):
                            continue
                
                group_passed = False
                break
                
            if group_passed:
                return group
                
        return None
