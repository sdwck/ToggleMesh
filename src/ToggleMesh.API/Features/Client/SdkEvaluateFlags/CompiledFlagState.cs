using ToggleMesh.Common.Rules;

namespace ToggleMesh.API.Features.Client.SdkEvaluateFlags;

public record CompiledFlagState(string Key, bool IsEnabled, int? RolloutPercentage, bool IsClientSideExposed, CompiledRuleGroup[] Groups);