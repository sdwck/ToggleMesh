namespace ToggleMesh.API.Features.Flags.Get;

public record GetFlagResponse(string Key, bool IsEnabled, IEnumerable<RuleDto> Rules, int? RolloutPercentage = null, long TrueCount = 0, long FalseCount = 0);

public record RuleDto(int GroupId, string Attribute, string Operator, string Value);