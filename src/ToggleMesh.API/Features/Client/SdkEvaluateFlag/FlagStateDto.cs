using ToggleMesh.Common.Rules;

namespace ToggleMesh.API.Features.Client.SdkEvaluateFlag;

public record FlagStateDto(
    string Key, 
    bool IsEnabled, 
    int? RolloutPercentage, 
    bool IsClientSideExposed, 
    List<RuleDto> Rules,
    string[]? ContextPartitionKeys,
    Dictionary<string, int>? ContextualRollouts,
    bool IsExperimentActive = false);