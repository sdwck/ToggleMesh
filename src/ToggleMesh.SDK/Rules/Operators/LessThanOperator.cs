namespace ToggleMesh.SDK.Rules.Operators;

internal class LessThanOperator : IRuleOperator
{
    public string Name => "LessThan";
    public bool Evaluate(string userValue, string ruleValue) => 
        double.TryParse(userValue, out var u) && double.TryParse(ruleValue, out var r) && u < r;
}