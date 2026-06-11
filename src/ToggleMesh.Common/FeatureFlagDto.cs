using ToggleMesh.Common.Rules;

namespace ToggleMesh.Common;

public sealed record FeatureFlagDto(string Key, bool IsEnabled, IEnumerable<RuleDto> Rules, int? RolloutPercentage = null);