import json
import os
import pytest
from unittest.mock import patch
from togglemesh.client import ToggleMeshClient
from togglemesh.models import FeatureFlagDto, RuleDto, VariationWeight, ToggleMeshOptions

FIXTURE_PATH = os.path.join(os.path.dirname(__file__), "..", "..", "..", "tests", "test-suite", "evaluation-fixtures.json")

def load_fixtures():
    with open(FIXTURE_PATH, "r", encoding="utf-8") as f:
        return json.load(f)

fixtures_data = load_fixtures()

test_cases = []
for scenario in fixtures_data.get("scenarios", []):
    for eval_case in scenario.get("evaluations", []):
        test_cases.append((scenario, eval_case))

@pytest.mark.parametrize("scenario, eval_case", test_cases, ids=[f"{s['name']}-{e['flagKey']}-{e['identity']}" for s, e in test_cases])
def test_universal_evaluations(scenario, eval_case):
    options = ToggleMeshOptions(server_key="test-key", base_url="http://localhost")
    with patch('togglemesh.client.ToggleMeshClient._sync_state'), \
         patch('togglemesh.client.ToggleMeshClient._start_threads'):
        client = ToggleMeshClient(options)
    
    client._stop_event.set()
    client._is_running = True
    
    for flag_data in scenario.get("flags", []):
        rules = []
        for r in flag_data.get("rules", []):
            rollout = None
            if "rollout" in r and r["rollout"]:
                rollout = [VariationWeight(variation_id=w["variationId"], weight=w["weight"]) for w in r["rollout"]]
            rules.append(RuleDto(
                priority=r.get("priority", 0),
                group_id=r.get("groupId", 0),
                attribute=r.get("attribute", ""),
                operator=r.get("operator", "False"),
                value=str(r.get("value", "")),
                rollout=rollout
            ))
            
        ft_rollout = []
        for w in flag_data.get("fallthroughRollout", []):
            ft_rollout.append(VariationWeight(variation_id=w["variationId"], weight=w["weight"]))
            
        dto = FeatureFlagDto(
            key=flag_data.get("key"),
            is_enabled=flag_data.get("isEnabled", False),
            off_variation_id=flag_data.get("offVariationId"),
            fallthrough_rollout=ft_rollout,
            individual_targets=flag_data.get("individualTargets"),
            contextual_rollouts=flag_data.get("contextualRollouts"),
            context_partition_keys=flag_data.get("contextPartitionKeys"),
            is_experiment_active=flag_data.get("isExperimentActive", False),
            variations=flag_data.get("variations"),
            rules=rules
        )
        client._cache_flag(dto)
        
    flag_key = eval_case["flagKey"]
    identity = eval_case["identity"]
    context = eval_case.get("context", {})
    expected = eval_case["expectedValue"]
    val_type = eval_case["type"]
    
    if val_type == "string":
        result = client.get_variation(flag_key, "default", identity=identity, context=context)
        assert result == expected
    elif val_type == "boolean":
        result = client.is_enabled(flag_key, False, identity=identity, context=context)
        assert str(result).lower() == str(expected).lower()
    elif val_type == "number":
        result = client.get_variation(flag_key, "0.0", identity=identity, context=context)
        assert float(result) == float(expected)
    elif val_type == "json":
        result = client.get_variation(flag_key, "{}", identity=identity, context=context)
        assert json.loads(result) == json.loads(expected)
