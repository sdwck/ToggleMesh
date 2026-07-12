package togglemesh

import (
	"log/slog"
	"net/http"
)

type ToggleMeshOptions struct {
	BaseURL                string
	APIKey                 string
	RefreshIntervalSeconds int
	DisableSSLVerification   bool
	UseFallbackFile          bool
	FallbackFilePath         string
	IsMetricsEnabled         *bool
	AnalyticsChannelCapacity int
	MetricsBufferCapacity    int
	MaxBatchSize             int
	Logger                   *slog.Logger
	HTTPClient               *http.Client
}

type VariationWeight struct {
	VariationID string `json:"variationId"`
	Weight      int    `json:"weight"`
}

type RuleDto struct {
	Priority  int               `json:"priority"`
	GroupID   int               `json:"groupId"`
	Attribute string            `json:"attribute"`
	Operator  string            `json:"operator"`
	Value     string            `json:"value"`
	Rollout   []VariationWeight `json:"rollout"`
}

type FeatureFlagDto struct {
	Key                  string                       `json:"key"`
	IsEnabled            bool                         `json:"isEnabled"`
	IsExperimentActive   bool                         `json:"isExperimentActive"`
	Rules                []RuleDto                    `json:"rules"`
	OffVariationID       *string                      `json:"offVariationId"`
	FallthroughRollout   []VariationWeight            `json:"fallthroughRollout"`
	Variations           map[string]string            `json:"variations"`
	ContextualRollouts   map[string][]VariationWeight `json:"contextualRollouts"`
	ContextPartitionKeys []string                     `json:"contextPartitionKeys"`
	IndividualTargets    map[string]string            `json:"individualTargets"`
}

type SegmentDto struct {
	ID    string    `json:"id"`
	Name  string    `json:"name"`
	Rules []RuleDto `json:"rules"`
}

type SdkGetFlagsResponse struct {
	Flags    []FeatureFlagDto `json:"flags"`
	Segments []SegmentDto     `json:"segments"`
}
