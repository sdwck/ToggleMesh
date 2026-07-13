using System.Collections.Concurrent;
using ToggleMesh.Common.Metrics;

namespace ToggleMesh.Common.Rules;

public class CachedFlag
{
    public string Key { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public Guid? OffVariationId { get; init; }
    public VariationWeight[] FallthroughRollout { get; init; } = [];
    public Dictionary<string, Guid>? IndividualTargets { get; init; }
    public Dictionary<string, object>? ContextualRolloutsTree { get; init; }
    public bool HasContextualRollouts { get; init; }
    public string[]? ContextPartitionKeys { get; init; }
    public bool IsExperimentActive { get; init; }
    public Dictionary<Guid, string>? Variations { get; init; }
    public CompiledRuleGroup[] Groups { get; init; } = [];
    public FeatureFlagDto OriginalDto { get; init; } = null!;
    public ConcurrentDictionary<Guid, object> ParsedJsonVariations { get; } = new();
    public Guid? FastResultVariationId;
    public bool FastBoolResult;
    public bool HasFastPath;
    public bool HasRolloutOnlyPath;
    public EvaluationStrategy Strategy;
    public Guid? TrueVariationId;
    public FlagMetrics? Metrics;
    public long FastMetricsCount;
}
