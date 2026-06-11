namespace ToggleMesh.Common.Rules.Operators;

public class ContainsOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) => 
        userValue.Contains(ruleValue, StringComparison.OrdinalIgnoreCase);
}