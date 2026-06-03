// ReSharper disable ForCanBeConvertedToForeach

using ToggleMesh.SDK.Contexts;

namespace ToggleMesh.SDK.Rules;

public class RuleEngine : IRuleEngine
{
    private readonly Dictionary<string, IRuleOperator> _operators;

    public RuleEngine(IEnumerable<IRuleOperator> operators)
    {
        _operators = operators.ToDictionary(o => o.Name, o => o, StringComparer.OrdinalIgnoreCase);
    }

    public bool Evaluate<TAccessor>(CompiledRuleGroup[] groups, ref EvaluationContext<TAccessor> context) where TAccessor : IContextAccessor
    {
        if (groups.Length == 0)
            return true;

        for (var i = 0; i < groups.Length; i++)
        {
            var rules = groups[i].Rules;
            var groupPassed = true;

            for (var j = 0; j < rules.Length; j++)
            {
                var rule = rules[j];

                if (context.TryGetValue(rule.Attribute, out var userValue) &&
                    rule.Operator.Evaluate(userValue!, rule.Value))
                    continue;

                groupPassed = false;
                break;
            }

            if (groupPassed)
                return true;
        }

        return false;
    }

    public CompiledRuleGroup[] CompileRules(IEnumerable<RuleDto>? rules)
    {
        if (rules is null)
            return [];

        return rules
            .GroupBy(x => x.GroupId)
            .Select(g => new CompiledRuleGroup(
                g.Select(r => new CompiledRule(
                    r.Attribute,
                    _operators.GetValueOrDefault(r.Operator) ?? new FalseOperator(), 
                    r.Value)
                ).ToArray())
            ).ToArray();
    }
    
    private class FalseOperator : IRuleOperator
    {
        public string Name => "False";

        public bool Evaluate(string userValue, string ruleValue) => false;
    }
}