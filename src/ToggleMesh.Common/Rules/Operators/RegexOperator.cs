using System.Text.RegularExpressions;

namespace ToggleMesh.Common.Rules.Operators;

public class RegexOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) 
    {
        try
        {
            return Regex.IsMatch(userValue, ruleValue, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}