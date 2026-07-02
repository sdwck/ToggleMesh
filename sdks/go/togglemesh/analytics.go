package togglemesh

import (
	"bytes"
	"context"
	"encoding/json"
	"net/http"
	"sync"
	"time"
)

type FlagMetrics struct {
	TrueCount  int64
	FalseCount int64
}

type EventDto struct {
	Type       int            `json:"Type"`
	Timestamp  int64          `json:"Timestamp"`
	Identity   string         `json:"Identity"`
	FlagKey    *string        `json:"FlagKey,omitempty"`
	Result     *bool          `json:"Result,omitempty"`
	EventName  *string        `json:"EventName,omitempty"`
	Value      *float64       `json:"Value,omitempty"`
	Properties map[string]any `json:"Properties,omitempty"`
}

type analyticsQueue struct {
	client        *ToggleMeshClient
	metricsBuffer map[string]*FlagMetrics
	metricsMu     sync.Mutex
	eventsQueue   chan EventDto
}

func newAnalyticsQueue(client *ToggleMeshClient) *analyticsQueue {
	q := &analyticsQueue{
		client:        client,
		metricsBuffer: make(map[string]*FlagMetrics),
		eventsQueue:   make(chan EventDto, client.options.AnalyticsChannelCapacity),
	}
	go q.metricsWorker()
	go q.eventsWorker()
	return q
}

func (q *analyticsQueue) updateMetrics(flagKey string, result bool) {
	if q.client.options.IsMetricsEnabled != nil && !*q.client.options.IsMetricsEnabled {
		return
	}
	q.metricsMu.Lock()
	defer q.metricsMu.Unlock()

	m, ok := q.metricsBuffer[flagKey]
	if !ok {
		if len(q.metricsBuffer) >= q.client.options.MetricsBufferCapacity {
			return
		}
		m = &FlagMetrics{}
		q.metricsBuffer[flagKey] = m
	}
	if result {
		m.TrueCount++
	} else {
		m.FalseCount++
	}
}

func (q *analyticsQueue) queueEvent(eventType int, identity string, flagKey string, result *bool, eventName string, properties map[string]any, value *float64) {
	if q.client.options.IsMetricsEnabled != nil && !*q.client.options.IsMetricsEnabled {
		return
	}
	evt := EventDto{
		Type:       eventType,
		Timestamp:  time.Now().UnixMilli(),
		Identity:   identity,
		Properties: properties,
	}
	if flagKey != "" {
		evt.FlagKey = &flagKey
	}
	if result != nil {
		evt.Result = result
	}
	if eventName != "" {
		evt.EventName = &eventName
	}
	if value != nil {
		evt.Value = value
	}

	select {
	case q.eventsQueue <- evt:
	default:
	}
}

func (q *analyticsQueue) metricsWorker() {
	ticker := time.NewTicker(10 * time.Second)
	defer ticker.Stop()

	for range ticker.C {
		q.flushMetrics()
	}
}

func (q *analyticsQueue) eventsWorker() {
	ticker := time.NewTicker(10 * time.Second)
	defer ticker.Stop()

	for {
		select {
		case <-ticker.C:
			q.flushEvents()
		}
	}
}

func (q *analyticsQueue) flushMetrics() {
	q.metricsMu.Lock()
	if len(q.metricsBuffer) == 0 {
		q.metricsMu.Unlock()
		return
	}

	payload := make([]map[string]any, 0, len(q.metricsBuffer))
	for key, m := range q.metricsBuffer {
		if m.TrueCount > 0 || m.FalseCount > 0 {
			payload = append(payload, map[string]any{
				"Key":        key,
				"TrueCount":  m.TrueCount,
				"FalseCount": m.FalseCount,
			})
			m.TrueCount = 0
			m.FalseCount = 0
		}
	}
	q.metricsMu.Unlock()

	if len(payload) == 0 {
		return
	}

	body, _ := json.Marshal(payload)
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	
	req, _ := http.NewRequestWithContext(ctx, "POST", q.client.options.BaseURL+"/api/v1/sdk/metrics", bytes.NewBuffer(body))
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("x-api-key", q.client.options.APIKey)

	resp, err := q.client.options.HTTPClient.Do(req)
	if err == nil && resp != nil {
		_ = resp.Body.Close()
	}
}

func (q *analyticsQueue) flushEvents() {
	var batch []EventDto
	for len(batch) < q.client.options.MaxBatchSize {
		select {
		case evt := <-q.eventsQueue:
			batch = append(batch, evt)
		default:
			goto Send
		}
	}
Send:
	if len(batch) == 0 {
		return
	}

	body, _ := json.Marshal(map[string]any{"Events": batch})
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	
	req, _ := http.NewRequestWithContext(ctx, "POST", q.client.options.BaseURL+"/api/v1/sdk/events", bytes.NewBuffer(body))
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("x-api-key", q.client.options.APIKey)

	resp, err := q.client.options.HTTPClient.Do(req)
	if err == nil && resp != nil {
		_ = resp.Body.Close()
	}
}
