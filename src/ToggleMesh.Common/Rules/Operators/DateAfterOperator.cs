namespace ToggleMesh.Common.Rules.Operators;

public class DateAfterOperator : RuleOperatorBase
{
    public override string Name => "DateAfter";
    public override object? Compile(string ruleValue) => 
        DateTime.TryParse(ruleValue, out var r) ? r : null;

    public override bool Evaluate(string userValue, object? compiledRuleValue) 
    {
        if (compiledRuleValue is DateTime r && DateTime.TryParse(userValue, out var u))
            return u > r;
        return false;
    }
}