namespace ToggleMesh.Common.Rules.Operators;

public class EndsWithOperator : RuleOperatorBase
{
    public override string Name => "EndsWith";
    public override bool Evaluate(string userValue, object? compiledRuleValue) => 
        compiledRuleValue is string str && userValue.EndsWith(str, StringComparison.OrdinalIgnoreCase);
}