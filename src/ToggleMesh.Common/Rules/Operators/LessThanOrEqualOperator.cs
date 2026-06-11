namespace ToggleMesh.Common.Rules.Operators;

public class LessThanOrEqualOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        double.TryParse(userValue, out var u) && double.TryParse(ruleValue, out var r) && u <= r;
}