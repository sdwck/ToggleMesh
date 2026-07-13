namespace ToggleMesh.Common.Rules;

public record RuleDto(int Priority, int GroupId, string Attribute, string Operator, string Value, VariationWeight[]? Rollout = null);
