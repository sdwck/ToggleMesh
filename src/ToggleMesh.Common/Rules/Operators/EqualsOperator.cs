namespace ToggleMesh.Common.Rules.Operators;

public class EqualsOperator : RuleOperatorBase
{
    public override string Name => "Equals";
    public override bool Evaluate(string userValue, object? compiledRuleValue) => 
        compiledRuleValue is string str && userValue.Equals(str, StringComparison.OrdinalIgnoreCase);
}