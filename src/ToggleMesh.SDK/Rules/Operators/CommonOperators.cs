namespace ToggleMesh.SDK.Rules.Operators;

public class EqualsOperator : IRuleOperator
{
    public string Name => "Equals";
    public bool Evaluate(string userValue, string ruleValue) => 
        userValue.Equals(ruleValue, StringComparison.OrdinalIgnoreCase);
}

public class NotEqualsOperator : IRuleOperator
{
    public string Name => "NotEquals";
    public bool Evaluate(string userValue, string ruleValue) => 
        !userValue.Equals(ruleValue, StringComparison.OrdinalIgnoreCase);
}

public class ContainsOperator : IRuleOperator
{
    public string Name => "Contains";
    public bool Evaluate(string userValue, string ruleValue) => 
        userValue.Contains(ruleValue, StringComparison.OrdinalIgnoreCase);
}

public class StartsWithOperator : IRuleOperator
{
    public string Name => "StartsWith";
    public bool Evaluate(string userValue, string ruleValue) => 
        userValue.StartsWith(ruleValue, StringComparison.OrdinalIgnoreCase);
}

public class EndsWithOperator : IRuleOperator
{
    public string Name => "EndsWith";
    public bool Evaluate(string userValue, string ruleValue) => 
        userValue.EndsWith(ruleValue, StringComparison.OrdinalIgnoreCase);
}

public class GreaterThanOperator : IRuleOperator
{
    public string Name => "GreaterThan";
    public bool Evaluate(string userValue, string ruleValue) => 
        double.TryParse(userValue, out var u) && double.TryParse(ruleValue, out var r) && u > r;
}

public class LessThanOperator : IRuleOperator
{
    public string Name => "LessThan";
    public bool Evaluate(string userValue, string ruleValue) => 
        double.TryParse(userValue, out var u) && double.TryParse(ruleValue, out var r) && u < r;
}

public class InListOperator : IRuleOperator
{
    public string Name => "InList";
    public bool Evaluate(string userValue, string ruleValue)
    {
        var list = ruleValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return list.Contains(userValue, StringComparer.OrdinalIgnoreCase);
    }
}