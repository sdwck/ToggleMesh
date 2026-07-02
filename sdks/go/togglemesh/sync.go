package togglemesh

import (
	"bufio"
	"bytes"
	"context"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"math"
	"math/rand"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"time"
)

func (c *ToggleMeshClient) syncState() error {
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	req, err := http.NewRequestWithContext(ctx, "GET", c.options.BaseURL+"/api/v1/sdk/flags", nil)
	if err != nil {
		return err
	}

	req.Header.Set("x-api-key", c.options.APIKey)
	req.Header.Set("x-sdk-version", "go-0.2.4")

	resp, err := c.options.HTTPClient.Do(req)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	if resp.StatusCode != 200 {
		return fmt.Errorf("unexpected status code: %d", resp.StatusCode)
	}

	var data SdkGetFlagsResponse
	if err := json.NewDecoder(resp.Body).Decode(&data); err != nil {
		return err
	}

	c.mu.Lock()
	c.flagsCache = make(map[string]CachedFlag)
	for _, f := range data.Flags {
		parsed := make(map[string]int)
		if len(f.ContextPartitionKeys) > 0 && f.ContextualRollouts != nil {
			for k, v := range f.ContextualRollouts {
				var d map[string]any
				if err := json.Unmarshal([]byte(k), &d); err == nil {
					var parts []string
					for _, key := range f.ContextPartitionKeys {
						if val, ok := d[key]; ok {
							parts = append(parts, fmt.Sprintf("%v", val))
						} else {
							parts = append(parts, "null")
						}
					}
					parsed[strings.Join(parts, "|")] = v
				}
			}
		}
		
		c.flagsCache[f.Key] = CachedFlag{
			Key:                      f.Key,
			IsEnabled:                f.IsEnabled,
			RolloutPercentage:        f.RolloutPercentage,
			ContextualRollouts:       f.ContextualRollouts,
			ParsedContextualRollouts: parsed,
			IsExperimentActive:       f.IsExperimentActive,
			Groups:                   c.ruleEngine.CompileRules(f.Rules),
			OriginalDto:              f,
		}
	}

	c.segmentsCache = make(map[string]CachedSegment)
	for _, s := range data.Segments {
		c.segmentsCache[s.ID] = CachedSegment{
			ID:     s.ID,
			Name:   s.Name,
			Groups: c.ruleEngine.CompileRules(s.Rules),
		}
	}
	c.mu.Unlock()

	c.saveFallback(data)
	return nil
}

func (c *ToggleMeshClient) resolveFallbackPath() string {
	if c.options.FallbackFilePath != "" {
		return c.options.FallbackFilePath
	}
	hash := sha256.Sum256([]byte(c.options.APIKey))
	hashStr := hex.EncodeToString(hash[:])[:12]
	cwd, _ := os.Getwd()
	return filepath.Join(cwd, ".togglemesh", hashStr+".json")
}

func (c *ToggleMeshClient) saveFallback(data SdkGetFlagsResponse) {
	if !c.options.UseFallbackFile {
		return
	}
	path := c.resolveFallbackPath()
	os.MkdirAll(filepath.Dir(path), 0755)
	b, err := json.Marshal(data)
	if err == nil {
		_ = os.WriteFile(path+".tmp", b, 0644)
		_ = os.Rename(path+".tmp", path)
	}
}

func (c *ToggleMeshClient) LoadFallback() {
	if !c.options.UseFallbackFile {
		return
	}
	path := c.resolveFallbackPath()
	b, err := os.ReadFile(path)
	if err != nil {
		return
	}
	var data SdkGetFlagsResponse
	if err := json.Unmarshal(b, &data); err != nil {
		return
	}
	
	c.mu.Lock()
	c.flagsCache = make(map[string]CachedFlag)
	for _, f := range data.Flags {
		c.flagsCache[f.Key] = CachedFlag{
			Key:                f.Key,
			IsEnabled:          f.IsEnabled,
			RolloutPercentage:  f.RolloutPercentage,
			IsExperimentActive: f.IsExperimentActive,
			Groups:             c.ruleEngine.CompileRules(f.Rules),
			OriginalDto:        f,
		}
	}
	c.segmentsCache = make(map[string]CachedSegment)
	for _, s := range data.Segments {
		c.segmentsCache[s.ID] = CachedSegment{
			ID:     s.ID,
			Name:   s.Name,
			Groups: c.ruleEngine.CompileRules(s.Rules),
		}
	}
	c.mu.Unlock()
}

func (c *ToggleMeshClient) startSSE() {
	go func() {
		backoff := 1.0
		for {
			err := c.connectSSE()
			if err != nil {
				if c.options.Logger != nil {
					c.options.Logger.Error("SSE connection dropped", "error", err)
				}
			}

			jitter := rand.Float64()
			waitTime := time.Duration((backoff + jitter) * float64(time.Second))
			time.Sleep(waitTime)

			backoff = math.Min(backoff*2.0, 30.0)

			_ = c.syncState()
		}
	}()
}

func (c *ToggleMeshClient) connectSSE() error {
	req, err := http.NewRequest("GET", c.options.BaseURL+"/api/v1/stream", nil)
	if err != nil {
		return err
	}

	req.Header.Set("Accept", "text/event-stream")
	req.Header.Set("x-api-key", c.options.APIKey)

	resp, err := c.options.HTTPClient.Do(req)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	if resp.StatusCode != 200 {
		return fmt.Errorf("SSE stream returned %d", resp.StatusCode)
	}

	reader := bufio.NewReader(resp.Body)
	for {
		line, err := reader.ReadBytes('\n')
		if err != nil {
			if errors.Is(err, io.EOF) {
				return nil
			}
			return err
		}

		line = bytes.TrimSpace(line)
		if len(line) == 0 {
			continue
		}

		if bytes.HasPrefix(line, []byte("data: ")) {
			payload := bytes.TrimPrefix(line, []byte("data: "))
			
			type SSEEvent struct {
				EventName string `json:"EventName"`
			}
			var evt SSEEvent
			if err := json.Unmarshal(payload, &evt); err == nil {
				if evt.EventName == "FlagUpdated" || evt.EventName == "StateReloadRequired" || evt.EventName == "SegmentUpdated" {
					_ = c.syncState()
				}
			} else {
				str := string(payload)
				if strings.Contains(str, "FlagUpdated") || strings.Contains(str, "StateReloadRequired") {
					_ = c.syncState()
				}
			}
		}
	}
}
