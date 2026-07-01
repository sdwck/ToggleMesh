namespace ToggleMesh.Common.Rules;

public class CachedFlag
{
    public string Key { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public int? RolloutPercentage { get; init; }
    public Dictionary<string, int>? ContextualRollouts { get; init; }
    public Dictionary<string, int>? ParsedContextualRollouts { get; init; }
    public bool IsExperimentActive { get; init; }
    public CompiledRuleGroup[] Groups { get; init; } = [];
    public FeatureFlagDto OriginalDto { get; init; } = null!;
}