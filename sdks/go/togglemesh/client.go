package togglemesh

import (
	"crypto/tls"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"strings"
	"sync"
)

type ToggleMeshClient struct {
	options       *ToggleMeshOptions
	flagsCache    map[string]CachedFlag
	segmentsCache map[string]CachedSegment
	mu            sync.RWMutex
	ruleEngine    *RuleEngine
	analytics     *analyticsQueue
}

func NewClient(options *ToggleMeshOptions) (*ToggleMeshClient, error) {
	if options == nil {
		return nil, errors.New("options cannot be nil")
	}
	if options.BaseURL == "" || options.APIKey == "" {
		return nil, errors.New("BaseURL and APIKey are required")
	}
	
	options.BaseURL = strings.TrimRight(options.BaseURL, "/")
	
	if options.HTTPClient == nil {
		tr := &http.Transport{
			TLSClientConfig: &tls.Config{InsecureSkipVerify: options.DisableSSLVerification},
		}
		options.HTTPClient = &http.Client{Transport: tr}
	}

	if options.AnalyticsChannelCapacity <= 0 {
		options.AnalyticsChannelCapacity = 10000
	}
	if options.MetricsBufferCapacity <= 0 {
		options.MetricsBufferCapacity = 10000
	}
	if options.MaxBatchSize <= 0 {
		options.MaxBatchSize = 2000
	}
	if options.IsMetricsEnabled == nil {
		t := true
		options.IsMetricsEnabled = &t
	}

	client := &ToggleMeshClient{
		options:       options,
		flagsCache:    make(map[string]CachedFlag),
		segmentsCache: make(map[string]CachedSegment),
	}
	
	client.ruleEngine = NewRuleEngine(client)
	client.analytics = newAnalyticsQueue(client)

	client.LoadFallback()

	if err := client.syncState(); err != nil {
		fmt.Printf("ERROR: Failed initial state sync: %v\n", err)
		if options.Logger != nil {
			options.Logger.Error("Failed initial state sync", "error", err)
		}
	}

	client.startSSE()

	return client, nil
}

func (c *ToggleMeshClient) getSegmentRules(segmentID string) []CompiledRuleGroup {
	c.mu.RLock()
	defer c.mu.RUnlock()
	
	if seg, ok := c.segmentsCache[segmentID]; ok {
		return seg.Groups
	}
	return nil
}

func (c *ToggleMeshClient) getVariationInternal(flagKey string, identity string, context map[string]any, defaultValue string) string {
	c.mu.RLock()
	flag, exists := c.flagsCache[flagKey]
	c.mu.RUnlock()

	if !exists {
		return defaultValue
	}

	if context == nil {
		context = make(map[string]any)
	}

	var variationId *string

	if !flag.IsEnabled {
		if flag.OriginalDto.OffVariationID != nil {
			variationId = flag.OriginalDto.OffVariationID
		}
	} else {
		if flag.OriginalDto.IndividualTargets != nil && identity != "" {
			if v, ok := flag.OriginalDto.IndividualTargets[identity]; ok {
				variationId = &v
			}
		}

		if variationId == nil {
			activeRollout := flag.OriginalDto.FallthroughRollout
			if len(flag.ParsedContextualRollouts) > 0 && len(flag.OriginalDto.ContextPartitionKeys) > 0 {
				var parts []string
				for _, key := range flag.OriginalDto.ContextPartitionKeys {
					if val, ok := context[key]; ok {
						parts = append(parts, fmt.Sprintf("%v", val))
					} else {
						parts = append(parts, "null")
					}
				}
				sliceKey := strings.Join(parts, "|")
				if overrideRollout, ok := flag.ParsedContextualRollouts[sliceKey]; ok {
					activeRollout = overrideRollout
				}
			}

			if len(flag.Groups) == 0 {
				variationId = evaluateRollout(activeRollout, flagKey, identity)
			} else {
				matchedGroup := c.ruleEngine.Evaluate(flag.Groups, context)
				if matchedGroup != nil {
					variationId = evaluateRollout(matchedGroup.Rollout, flagKey, identity)
				} else {
					variationId = evaluateRollout(activeRollout, flagKey, identity)
				}
			}
		}
	}

	if variationId != nil {
		c.analytics.updateMetrics(flagKey, *variationId)
		if identity != "" && flag.IsExperimentActive {
			c.analytics.queueEvent(0, identity, flagKey, nil, "", context, nil, variationId)
		}
		if val, ok := flag.OriginalDto.Variations[*variationId]; ok {
			return val
		}
	}

	return defaultValue
}

func (c *ToggleMeshClient) GetStringVariation(flagKey string, defaultValue string, opts ...EvalOption) string {
	options := applyOptions(opts)
	return c.getVariationInternal(flagKey, options.Identity, options.Context, defaultValue)
}

func (c *ToggleMeshClient) IsEnabled(flagKey string, defaultValue bool, opts ...EvalOption) bool {
	dvStr := "false"
	if defaultValue {
		dvStr = "true"
	}
	options := applyOptions(opts)
	val := c.getVariationInternal(flagKey, options.Identity, options.Context, dvStr)
	return strings.ToLower(val) == "true"
}

func (c *ToggleMeshClient) GetJsonVariation(flagKey string, target any, opts ...EvalOption) error {
	options := applyOptions(opts)
	val := c.getVariationInternal(flagKey, options.Identity, options.Context, "")
	if val == "" {
		return errors.New("flag not found or off variation is empty")
	}
	return json.Unmarshal([]byte(val), target)
}

func (c *ToggleMeshClient) Track(eventName string, opts ...EvalOption) {
	options := applyOptions(opts)
	if options.Identity == "" || eventName == "" {
		return
	}
	c.analytics.queueEvent(1, options.Identity, "", nil, eventName, options.Context, options.Value, nil)
}
