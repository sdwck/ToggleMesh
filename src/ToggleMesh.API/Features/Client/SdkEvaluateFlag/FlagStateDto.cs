using ToggleMesh.Common.Rules;
using ToggleMesh.Common;

namespace ToggleMesh.API.Features.Client.SdkEvaluateFlag;

public record FlagStateDto(
    string Key, 
    bool IsEnabled, 
    Guid? OffVariationId,
    VariationWeight[] FallthroughRollout,
    bool IsClientSideExposed, 
    List<RuleDto> Rules,
    string[]? ContextPartitionKeys,
    Dictionary<string, VariationWeight[]>? ContextualRollouts,
    Dictionary<string, Guid>? IndividualTargets,
    Dictionary<Guid, string>? Variations,
    bool IsExperimentActive = false);
