namespace ToggleMesh.API.Features.Flags.Get;

public record GetFlagResponse(string Key, bool IsEnabled, IEnumerable<RuleDto> Rules, IEnumerable<string> Tags, int? RolloutPercentage = null, long TrueCount = 0, long FalseCount = 0);