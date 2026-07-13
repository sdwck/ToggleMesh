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
    context?: Record<string, any>;
}

export interface EvalOptions {
    identity?: string;
    context?: Record<string, any>;
}

export interface TrackOptions extends EvalOptions {
    value?: number;
}

export interface VariationWeight {
    variationId: string;
    weight: number;
}

export interface RuleDto {
    priority: number;
    groupId: number;
    attribute: string;
    operator: string;
    value: string;
    rollout: VariationWeight[];
}

export interface FeatureFlagDto {
    key: string;
    isEnabled: boolean;
    isExperimentActive: boolean;
    rules: RuleDto[];
    offVariationId?: string;
    fallthroughRollout: VariationWeight[];
    variations: Record<string, string>;
    contextualRollouts?: Record<string, VariationWeight[]>;
    contextPartitionKeys?: string[];
    individualTargets?: Record<string, string>;
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
