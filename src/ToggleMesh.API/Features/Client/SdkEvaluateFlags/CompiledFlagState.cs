using ToggleMesh.Common.Rules;
using ToggleMesh.Common;

namespace ToggleMesh.API.Features.Client.SdkEvaluateFlags;

public record CompiledFlagState(
    string Key, 
    bool IsEnabled, 
    Guid? OffVariationId,
    VariationWeight[] FallthroughRollout,
    bool IsClientSideExposed, 
    CompiledRuleGroup[] Groups,
    string[]? ContextPartitionKeys,
    Dictionary<string, VariationWeight[]>? ContextualRollouts,
    Dictionary<string, Guid>? IndividualTargets,
    Dictionary<Guid, string>? Variations,
    bool IsExperimentActive = false);
