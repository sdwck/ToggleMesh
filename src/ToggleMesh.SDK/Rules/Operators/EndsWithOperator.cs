namespace ToggleMesh.SDK.Rules.Operators;

internal class EndsWithOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        userValue.EndsWith(ruleValue, StringComparison.OrdinalIgnoreCase);
}