namespace ToggleMesh.Common.Rules.Operators;

public class InListOperator : RuleOperatorBase
{
    public override string Name => "InList";
    public override object? Compile(string ruleValue)
    {
        if (string.IsNullOrWhiteSpace(ruleValue))
            return null;
        
        var parts = ruleValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new HashSet<string>(parts, StringComparer.OrdinalIgnoreCase);
    }

    public override bool Evaluate(string userValue, object? compiledRuleValue) =>
        compiledRuleValue is HashSet<string> set && set.Contains(userValue);
}