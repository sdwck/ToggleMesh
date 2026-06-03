namespace ToggleMesh.SDK.Rules;

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
        
        var ruleGroups = listRules.GroupBy(rule => rule.GroupId);
        
        return ruleGroups.Any(group => 
            group.All(rule => 
                context.TryGetValue(rule.Attribute, out var userValue) &&
                _operators.TryGetValue(rule.Operator, out var op) &&
                op.Evaluate(userValue, rule.Value)
            )
        );
    }
}