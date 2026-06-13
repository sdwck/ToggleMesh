using System.Text.RegularExpressions;

namespace ToggleMesh.Common.Rules.Operators;

public class RegexOperator : RuleOperatorBase
{
    private static readonly TimeSpan EvaluationTimeout = TimeSpan.FromMilliseconds(100);

    public override object? Compile(string ruleValue)
    {
        if (string.IsNullOrWhiteSpace(ruleValue))
            return null;

        try
        {
            return new Regex(
                ruleValue, 
                RegexOptions.IgnoreCase | RegexOptions.Compiled, 
                EvaluationTimeout);
        }
        catch
        {
            return null;
        }
    }

    public override bool Evaluate(string? userValue, object? compiledRuleValue) 
    {
        if (userValue is null || compiledRuleValue is not Regex regex)
            return false;

        try
        {
            return regex.IsMatch(userValue);
        }
        catch
        {
            return false;
        }
    }
}