namespace ToggleMesh.SDK.Rules.Operators;

internal class StartsWithOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        userValue.StartsWith(ruleValue, StringComparison.OrdinalIgnoreCase);
}