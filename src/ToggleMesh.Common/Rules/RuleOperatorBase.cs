namespace ToggleMesh.Common.Rules;

public abstract class RuleOperatorBase : IRuleOperator
{
    protected RuleOperatorBase()
    {
        var typeName = GetType().Name;
        const string suffix = "Operator";
        
        if (typeName.EndsWith(suffix) && typeName.Length > suffix.Length)
            Name = typeName[..^suffix.Length]; 
        else
            Name = typeName;
    }

    public string Name { get; }

    public virtual object? Compile(string ruleValue) => ruleValue;
    public abstract bool Evaluate(string userValue, object? compiledRuleValue);
}