package togglemesh

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"testing"
)

type EvaluationFixture struct {
	Scenarios []struct {
		Name        string           `json:"name"`
		Flags       []FeatureFlagDto `json:"flags"`
		Evaluations []struct {
			FlagKey       string         `json:"flagKey"`
			Identity      string         `json:"identity"`
			Context       map[string]any `json:"context"`
			ExpectedValue string         `json:"expectedValue"`
			Type          string         `json:"type"`
		} `json:"evaluations"`
	} `json:"scenarios"`
}

func TestUniversalSuite(t *testing.T) {
	fixturePath := filepath.Join("..", "..", "..", "tests", "test-suite", "evaluation-fixtures.json")
	data, err := os.ReadFile(fixturePath)
	if err != nil {
		t.Fatalf("Failed to read fixture file: %v", err)
	}

	var fixtures EvaluationFixture
	if err := json.Unmarshal(data, &fixtures); err != nil {
		t.Fatalf("Failed to parse fixture file: %v", err)
	}

	options := &ToggleMeshOptions{
		BaseURL: "http://localhost",
		APIKey:  "test-key",
	}

	for _, scenario := range fixtures.Scenarios {
		t.Run(scenario.Name, func(t *testing.T) {
			client := &ToggleMeshClient{
				options:       options,
				flagsCache:    make(map[string]CachedFlag),
				segmentsCache: make(map[string]CachedSegment),
			}
			client.ruleEngine = NewRuleEngine(client)
			client.analytics = newAnalyticsQueue(client)

			for _, flagDto := range scenario.Flags {
				client.flagsCache[flagDto.Key] = CachedFlag{
					Key:                      flagDto.Key,
					IsEnabled:                flagDto.IsEnabled,
					ContextualRollouts:       flagDto.ContextualRollouts,
					ParsedContextualRollouts: make(map[string][]VariationWeight),
					IsExperimentActive:       flagDto.IsExperimentActive,
					Groups:                   client.ruleEngine.CompileRules(flagDto.Rules),
					OriginalDto:              flagDto,
				}
                cachedFlag := client.flagsCache[flagDto.Key]
                if len(flagDto.ContextualRollouts) > 0 && len(flagDto.ContextPartitionKeys) > 0 {
                    for k, v := range flagDto.ContextualRollouts {
                        var d map[string]any
                        if err := json.Unmarshal([]byte(k), &d); err == nil {
                            var parts []string
                            for _, pk := range flagDto.ContextPartitionKeys {
                                if val, ok := d[pk]; ok {
                                    parts = append(parts, fmt.Sprintf("%v", val))
                                } else {
                                    parts = append(parts, "null")
                                }
                            }
                            sliceKey := strings.Join(parts, "|")
                            cachedFlag.ParsedContextualRollouts[sliceKey] = v
                        }
                    }
                }
                client.flagsCache[flagDto.Key] = cachedFlag
			}

			for _, evalCase := range scenario.Evaluations {
				t.Run(fmt.Sprintf("%s-%s", evalCase.FlagKey, evalCase.Identity), func(t *testing.T) {
					result := client.GetStringVariation(evalCase.FlagKey, "default", WithIdentity(evalCase.Identity), WithContext(evalCase.Context))
					
					if evalCase.Type == "string" {
						if result != evalCase.ExpectedValue {
							t.Errorf("expected %s, got %s", evalCase.ExpectedValue, result)
						}
					} else if evalCase.Type == "boolean" {
						resBool := strings.ToLower(result) == "true"
						expBool := strings.ToLower(evalCase.ExpectedValue) == "true"
						if resBool != expBool {
							t.Errorf("expected %v, got %v", expBool, resBool)
						}
					} else if evalCase.Type == "number" {
						resNum, _ := strconv.ParseFloat(result, 64)
						expNum, _ := strconv.ParseFloat(evalCase.ExpectedValue, 64)
						if resNum != expNum {
							t.Errorf("expected %v, got %v", expNum, resNum)
						}
					} else if evalCase.Type == "json" {
						var resJson, expJson interface{}
						json.Unmarshal([]byte(result), &resJson)
						json.Unmarshal([]byte(evalCase.ExpectedValue), &expJson)
						
						rBytes, _ := json.Marshal(resJson)
						eBytes, _ := json.Marshal(expJson)
						if string(rBytes) != string(eBytes) {
							t.Errorf("expected %s, got %s", string(eBytes), string(rBytes))
						}
					}
				})
			}
		})
	}
}
