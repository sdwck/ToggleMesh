namespace ToggleMesh.Common.Rules.Operators;

public class EqualsOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, object? compiledRuleValue) => 
        compiledRuleValue is string str && userValue.Equals(str, StringComparison.OrdinalIgnoreCase);
}