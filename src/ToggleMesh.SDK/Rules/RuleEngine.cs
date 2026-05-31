namespace ToggleMesh.SDK.Rules;

public interface IRuleEngine
{
    bool Evaluate(IEnumerable<RuleDto> rules, IDictionary<string, string> context);
}

public record RuleDto(string Attribute, string Operator, string Value);

public class RuleEngine : IRuleEngine
{
    private readonly Dictionary<string, IRuleOperator> _operators;

    public RuleEngine(IEnumerable<IRuleOperator> operators)
    {
        _operators = operators.ToDictionary(o => o.Name, o => o, StringComparer.OrdinalIgnoreCase);
    }

    public bool Evaluate(IEnumerable<RuleDto>? rules, IDictionary<string, string> context)
    {
        var listRules = rules?.ToList();
        if (listRules == null || listRules.Count == 0)
            return true;

        foreach (var rule in listRules)
            if (!context.TryGetValue(rule.Attribute, out var userValue) ||
                !_operators.TryGetValue(rule.Operator, out var op) ||
                !op.Evaluate(userValue, rule.Value))
                return false;

        return true;
    }
}