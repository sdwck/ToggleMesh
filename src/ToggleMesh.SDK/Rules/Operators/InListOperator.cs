namespace ToggleMesh.SDK.Rules.Operators;

internal class InListOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue)
    {
        var list = ruleValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return list.Contains(userValue, StringComparer.OrdinalIgnoreCase);
    }
}