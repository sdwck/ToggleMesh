using NuGet.Versioning;

namespace ToggleMesh.SDK.Rules.Operators;

internal class SemVerGreaterThanOrEqualOperator : RuleOperatorBase
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