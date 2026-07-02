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

type RuleDto struct {
	GroupID   int    `json:"groupId"`
	Attribute string `json:"attribute"`
	Operator  string `json:"operator"`
	Value     string `json:"value"`
}

type FeatureFlagDto struct {
	Key                  string         `json:"key"`
	IsEnabled            bool           `json:"isEnabled"`
	IsExperimentActive   bool           `json:"isExperimentActive"`
	RolloutPercentage    *int           `json:"rolloutPercentage"`
	ContextPartitionKeys []string       `json:"contextPartitionKeys"`
	ContextualRollouts   map[string]int `json:"contextualRollouts"`
	Rules                []RuleDto      `json:"rules"`
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
