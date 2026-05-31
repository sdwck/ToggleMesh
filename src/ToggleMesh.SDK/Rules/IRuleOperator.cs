namespace ToggleMesh.SDK.Rules;

public interface IRuleOperator
{
    string Name { get; }
    bool Evaluate(string userValue, string ruleValue);
}