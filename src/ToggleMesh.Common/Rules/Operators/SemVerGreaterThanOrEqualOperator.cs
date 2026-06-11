using NuGet.Versioning;

namespace ToggleMesh.Common.Rules.Operators;

public class SemVerGreaterThanOrEqualOperator : RuleOperatorBase
{
    public override bool Evaluate(string userValue, string ruleValue) 
    {
        if (NuGetVersion.TryParse(userValue, out var u) && NuGetVersion.TryParse(ruleValue, out var r))
        {
            return u >= r;
        }
        return false;
    }
}