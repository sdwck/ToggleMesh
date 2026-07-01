package togglemesh

import (
	"crypto/tls"
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

	client := &ToggleMeshClient{
		options:       options,
		flagsCache:    make(map[string]CachedFlag),
		segmentsCache: make(map[string]CachedSegment),
	}
	
	client.ruleEngine = NewRuleEngine(client)
	client.analytics = newAnalyticsQueue(client)

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

func (c *ToggleMeshClient) IsEnabled(flagKey string, defaultValue bool, identity string, context map[string]any) bool {
	c.mu.RLock()
	flag, exists := c.flagsCache[flagKey]
	c.mu.RUnlock()

	if !exists {
		return defaultValue
	}

	if context == nil {
		context = make(map[string]any)
	}

	activeRolloutPercentage := flag.RolloutPercentage
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
		if overridePct, ok := flag.ParsedContextualRollouts[sliceKey]; ok {
			activeRolloutPercentage = &overridePct
		}
	}

	result := true
	if !flag.IsEnabled || (len(flag.Groups) > 0 && !c.ruleEngine.Evaluate(flag.Groups, context)) {
		result = false
	} else {
		result = evaluateRollout(activeRolloutPercentage, flagKey, identity)
	}

	c.analytics.updateMetrics(flagKey, result)
	if identity != "" && flag.IsExperimentActive {
		c.analytics.queueEvent(0, identity, flagKey, &result, "", context, nil)
	}

	return result
}

func (c *ToggleMeshClient) Track(eventName string, value float64, identity string, context map[string]any) {
	if identity == "" || eventName == "" {
		return
	}
	if context == nil {
		context = make(map[string]any)
	}
	
	c.analytics.queueEvent(1, identity, "", nil, eventName, context, &value)
}
