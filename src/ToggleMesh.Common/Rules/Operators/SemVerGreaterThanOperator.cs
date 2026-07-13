using NuGet.Versioning;
using ToggleMesh.Common.Utils;

namespace ToggleMesh.Common.Rules.Operators;

public class SemVerGreaterThanOperator : RuleOperatorBase
{
    public override string Name => "SemVerGreaterThan";
    public override object? Compile(string ruleValue) =>
        SemVerCache.TryParse(ruleValue, out var r) ? r : null;
    
    public override bool Evaluate(string userValue, object? compiledRuleValue) 
    {
        if (compiledRuleValue is NuGetVersion r && 
            SemVerCache.TryParse(userValue, out var u))
            return u > r;
        return false;
    }
}