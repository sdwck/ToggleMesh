namespace ToggleMesh.Common.Rules.Operators;

public class StartsWithOperator : RuleOperatorBase
{
    public override string Name => "StartsWith";
    public override bool Evaluate(string userValue, object? compiledRuleValue) => 
        compiledRuleValue is string str && 
        userValue.StartsWith(str, StringComparison.OrdinalIgnoreCase);
}