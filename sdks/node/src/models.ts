export interface ToggleMeshOptions {
    baseUrl: string;
    serverKey: string;
    refreshInterval?: number;
    isMetricsEnabled?: boolean;
    useFallbackFile?: boolean;
    fallbackFilePath?: string;
    analyticsChannelCapacity?: number;
    metricsBufferCapacity?: number;
    maxBatchSize?: number;
}

export interface ToggleMeshUser {
    identity: string;
    context?: Record<string, string>;
}

export interface RuleDto {
    groupId: number;
    attribute: string;
    operator: string;
    value: string;
}

export interface FeatureFlagDto {
    key: string;
    isEnabled: boolean;
    isExperimentActive: boolean;
    rolloutPercentage?: number;
    contextPartitionKeys?: string[];
    contextualRollouts?: Record<string, number>;
    rules: RuleDto[];
}

export interface SegmentDto {
    id: string;
    name: string;
    rules: RuleDto[];
}

export interface SdkGetFlagsResponse {
    flags: FeatureFlagDto[];
    segments: SegmentDto[];
}
