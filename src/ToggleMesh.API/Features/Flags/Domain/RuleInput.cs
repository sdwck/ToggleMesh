namespace ToggleMesh.API.Features.Flags.Domain;

public record RuleInput(int GroupId, string Attribute, string Operator, string Value, List<VariationWeight>? Rollout = null);
