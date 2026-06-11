namespace ToggleMesh.Common.Rules.Operators;

public class EqualsOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        userValue.Equals(ruleValue, StringComparison.OrdinalIgnoreCase);
}