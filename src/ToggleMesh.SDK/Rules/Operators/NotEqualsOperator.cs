namespace ToggleMesh.SDK.Rules.Operators;

internal class NotEqualsOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        !userValue.Equals(ruleValue, StringComparison.OrdinalIgnoreCase);
}