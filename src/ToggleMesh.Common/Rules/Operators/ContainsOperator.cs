namespace ToggleMesh.Common.Rules.Operators;

public class ContainsOperator : RuleOperatorBase
{
    public override string Name => "Contains";
    public override bool Evaluate(string userValue, object? compiledRuleValue) => 
        compiledRuleValue is string str && userValue.Contains(str, StringComparison.OrdinalIgnoreCase);
}