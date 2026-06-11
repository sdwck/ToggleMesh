namespace ToggleMesh.Common.Rules.Operators;

public class StartsWithOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        userValue.StartsWith(ruleValue, StringComparison.OrdinalIgnoreCase);
}