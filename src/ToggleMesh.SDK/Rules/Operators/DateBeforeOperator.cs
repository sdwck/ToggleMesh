namespace ToggleMesh.SDK.Rules.Operators;

internal class DateBeforeOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) 
    {
        if (DateTime.TryParse(userValue, out var u) && DateTime.TryParse(ruleValue, out var r))
        {
            return u < r;
        }
        return false;
    }
}