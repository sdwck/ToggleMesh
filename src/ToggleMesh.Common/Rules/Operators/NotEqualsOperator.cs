namespace ToggleMesh.Common.Rules.Operators;

public class NotEqualsOperator : RuleOperatorBase
{
    public override string Name => "NotEquals";
    public override bool Evaluate(string userValue, object? compiledRuleValue) => 
        compiledRuleValue is string str && !userValue.Equals(str, StringComparison.OrdinalIgnoreCase);
}