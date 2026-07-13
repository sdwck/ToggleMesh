using ToggleMesh.Common.Rules;

namespace ToggleMesh.Common;

public sealed record FeatureFlagDto(
    string Key, 
    bool IsEnabled, 
    IEnumerable<RuleDto> Rules, 
    Guid? OffVariationId = null,
    IEnumerable<VariationWeight>? FallthroughRollout = null, 
    Dictionary<Guid, string>? Variations = null,
    bool IsExperimentActive = false, 
    Dictionary<string, IEnumerable<VariationWeight>>? ContextualRollouts = null, 
    string[]? ContextPartitionKeys = null,
    Dictionary<string, Guid>? IndividualTargets = null,
    EvaluationStrategy EvaluationStrategy = EvaluationStrategy.Complex);
