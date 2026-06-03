namespace ToggleMesh.SDK.Rules.Operators;

internal class ContainsOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        userValue.Contains(ruleValue, StringComparison.OrdinalIgnoreCase);
}