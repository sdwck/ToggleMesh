namespace ToggleMesh.Common.Rules.Operators;

public class NotEqualsOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        !userValue.Equals(ruleValue, StringComparison.OrdinalIgnoreCase);
}