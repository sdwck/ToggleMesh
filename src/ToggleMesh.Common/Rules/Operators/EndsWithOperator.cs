namespace ToggleMesh.Common.Rules.Operators;

public class EndsWithOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        userValue.EndsWith(ruleValue, StringComparison.OrdinalIgnoreCase);
}