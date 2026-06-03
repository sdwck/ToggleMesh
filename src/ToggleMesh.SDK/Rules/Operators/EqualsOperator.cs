namespace ToggleMesh.SDK.Rules.Operators;

internal class EqualsOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        userValue.Equals(ruleValue, StringComparison.OrdinalIgnoreCase);
}