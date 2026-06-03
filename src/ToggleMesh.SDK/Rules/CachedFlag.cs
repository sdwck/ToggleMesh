using ToggleMesh.SDK.Clients;

namespace ToggleMesh.SDK.Rules;

internal class CachedFlag
{
    public string Key { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public int? RolloutPercentage { get; init; }
    public CompiledRuleGroup[] Groups { get; init; } = [];
    public ToggleMeshClient.FeatureFlagDto OriginalDto { get; init; } = null!;
}