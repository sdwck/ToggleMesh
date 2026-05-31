namespace ToggleMesh.API.Features.Flags.Get;

public record GetFlagResponse(string Key, bool IsEnabled, IEnumerable<RuleDto> Rules, int? RolloutPercentage = null);

public record RuleDto(string Attribute, string Operator, string Value);