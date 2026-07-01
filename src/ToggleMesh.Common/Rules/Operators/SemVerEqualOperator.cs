using NuGet.Versioning;

namespace ToggleMesh.Common.Rules.Operators;

public class SemVerEqualOperator : RuleOperatorBase
{
    public override string Name => "SemVerEqual";
    public override object? Compile(string ruleValue) =>
        NuGetVersion.TryParse(ruleValue, out var r) ? r : null;
    
    public override bool Evaluate(string userValue, object? compiledRuleValue) 
    {
        if (compiledRuleValue is NuGetVersion r && 
            NuGetVersion.TryParse(
                userValue, 
                out var u))
            return u == r;
        return false;
    }
}