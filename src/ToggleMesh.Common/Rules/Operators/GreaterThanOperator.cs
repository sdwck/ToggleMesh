namespace ToggleMesh.Common.Rules.Operators;

public class GreaterThanOperator : RuleOperatorBase
{
    public override string Name => "GreaterThan";
    public override object? Compile(string ruleValue) => 
        double.TryParse(ruleValue, out var r) ? r : null;

    public override bool Evaluate(string userValue, object? compiledRuleValue) => 
        compiledRuleValue is double r && double.TryParse(userValue, out var u) && u > r;
}