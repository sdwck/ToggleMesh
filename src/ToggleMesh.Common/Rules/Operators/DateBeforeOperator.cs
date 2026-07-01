namespace ToggleMesh.Common.Rules.Operators;

public class DateBeforeOperator : RuleOperatorBase
{
    public override string Name => "DateBefore";
    public override object? Compile(string ruleValue) => 
        DateTime.TryParse(ruleValue, out var r) ? r : null;

    public override bool Evaluate(string userValue, object? compiledRuleValue) 
    {
        if (compiledRuleValue is DateTime r && DateTime.TryParse(userValue, out var u))
            return u < r;
        return false;
    }
}