package togglemesh

import (
	"encoding/json"
	"strings"
)

type CompiledRule struct {
	Attribute    string
	Operator     RuleOperator
	CompiledValue any
	IsSegment    bool
}

type CompiledRuleGroup struct {
	Rules    []CompiledRule
	Priority int
	Rollout  []VariationWeight
}

type CachedFlag struct {
	Key                      string
	IsEnabled                bool
	ContextualRollouts       map[string][]VariationWeight
	ParsedContextualRollouts map[string][]VariationWeight
	IsExperimentActive       bool
	Groups                   []CompiledRuleGroup
	OriginalDto              FeatureFlagDto
}

type CachedSegment struct {
	ID     string
	Name   string
	Groups []CompiledRuleGroup
}

type RuleEngine struct {
	client *ToggleMeshClient
}

func NewRuleEngine(client *ToggleMeshClient) *RuleEngine {
	return &RuleEngine{client: client}
}

func (e *RuleEngine) CompileRules(rules []RuleDto) []CompiledRuleGroup {
	if len(rules) == 0 {
		return nil
	}

	groupsDict := make(map[int][]RuleDto)
	for _, r := range rules {
		groupsDict[r.GroupID] = append(groupsDict[r.GroupID], r)
	}

	var compiledGroups []CompiledRuleGroup
	for _, gRules := range groupsDict {
		var compiledRules []CompiledRule
		for _, r := range gRules {
			isSegment := strings.ToLower(r.Operator) == "insegment"
			var op RuleOperator
			if isSegment {
				op = &InSegmentOperator{Engine: e}
			} else {
				op = getOperator(r.Operator)
			}
			
			compiledRules = append(compiledRules, CompiledRule{
				Attribute:    r.Attribute,
				Operator:     op,
				CompiledValue: CompileValue(r.Operator, r.Value),
				IsSegment:    isSegment,
			})
		}
		compiledGroups = append(compiledGroups, CompiledRuleGroup{
			Rules:    compiledRules,
			Priority: gRules[0].Priority,
			Rollout:  gRules[0].Rollout,
		})
	}

	for i := 0; i < len(compiledGroups); i++ {
		for j := i + 1; j < len(compiledGroups); j++ {
			if compiledGroups[i].Priority > compiledGroups[j].Priority {
				compiledGroups[i], compiledGroups[j] = compiledGroups[j], compiledGroups[i]
			}
		}
	}

	return compiledGroups
}

func (e *RuleEngine) Evaluate(groups []CompiledRuleGroup, context map[string]any) *CompiledRuleGroup {
	if len(groups) == 0 {
		return nil
	}

	for _, group := range groups {
		groupMatched := true
		for _, rule := range group.Rules {
			if rule.IsSegment {
				segmentID := rule.CompiledValue.(string)
				segmentRules := e.client.getSegmentRules(segmentID)
				if segmentRules == nil || e.Evaluate(segmentRules, context) == nil {
					groupMatched = false
					break
				}
				continue
			}

			var ctxValue string
			if val, ok := context[rule.Attribute]; ok {
				switch v := val.(type) {
				case string:
					ctxValue = v
				default:
					b, _ := json.Marshal(v)
					ctxValue = string(b)
				}
				
				if !rule.Operator.Evaluate(ctxValue, rule.CompiledValue) {
					groupMatched = false
					break
				}
			} else {
				groupMatched = false
				break
			}
		}

		if groupMatched {
			return &group
		}
	}

	return nil
}
