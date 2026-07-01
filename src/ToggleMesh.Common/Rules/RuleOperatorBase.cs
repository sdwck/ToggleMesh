namespace ToggleMesh.Common.Rules;

public abstract class RuleOperatorBase : IRuleOperator
{
    public abstract string Name { get; }

    public virtual object? Compile(string ruleValue) => ruleValue;
    public abstract bool Evaluate(string userValue, object? compiledRuleValue);
}