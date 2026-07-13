// ReSharper disable ForCanBeConvertedToForeach

using ToggleMesh.Common.Contexts;

namespace ToggleMesh.Common.Rules;

public class RuleEngine : IRuleEngine
{
    private readonly Dictionary<string, IRuleOperator> _operators;
    private readonly ISegmentProvider? _segmentProvider;

    public RuleEngine(IEnumerable<IRuleOperator> operators, ISegmentProvider? segmentProvider = null)
    {
        _operators = operators.ToDictionary(o => o.Name, o => o, StringComparer.OrdinalIgnoreCase);
        _segmentProvider = segmentProvider;
    }

    public int Evaluate<TAccessor>(
        CompiledRuleGroup[] groups, 
        ref EvaluationContext<TAccessor> context) 
        where TAccessor : IContextAccessor
    {
        if (groups.Length == 0) 
            return -1;

        for (int i = 0; i < groups.Length; i++)
        {
            ref readonly var group = ref groups[i];
            var groupPassed = true;

            foreach (ref readonly var rule in group.Rules.AsSpan())
            {
                if (rule.IsSegmentRule)
                {
                    if (rule.CompiledValue is string segmentId && _segmentProvider != null)
                    {
                        var segmentRules = _segmentProvider.GetSegmentRules(segmentId);
                        if (segmentRules != null && Evaluate(segmentRules, ref context) >= 0) 
                            continue;
                    }
                }
                else if (context.TryGetValue(rule.Attribute, out var userValue) &&
                         rule.Operator.Evaluate(userValue!, rule.CompiledValue))
                    continue;

                groupPassed = false;
                break;
            }

            if (groupPassed) 
                return i;
        }

        return -1;
    }

    public CompiledRuleGroup[] CompileRules(IEnumerable<RuleDto>? rules)
    {
        if (rules is null)
            return [];

        return rules
            .GroupBy(x => x.GroupId)
            .OrderBy(g => g.First().Priority)
            .Select(g => new CompiledRuleGroup(
                g.Select(r =>
                {
                    IRuleOperator op;
                    var isSegment = false;
                    if (string.Equals(r.Operator, "InSegment", StringComparison.OrdinalIgnoreCase))
                    {
                        op = InSegmentOperator.Instance;
                        isSegment = true;
                    }
                    else
                    {
                        op = _operators.GetValueOrDefault(r.Operator) ?? FalseOperator.Instance;
                    }

                    return new CompiledRule(
                        r.Attribute,
                        op,
                        op.Compile(r.Value),
                        isSegment);
                }).ToArray(),
                g.First().Rollout)
            ).ToArray();
    }
    
    private sealed class FalseOperator : IRuleOperator
    {
        public static readonly FalseOperator Instance = new();
        
        private FalseOperator() { }
        
        public string Name => "False";
        
        public object? Compile(string ruleValue) => null;
        public bool Evaluate(string userValue, object? compiledRuleValue) => false;
    }

    private sealed class InSegmentOperator : IRuleOperator
    {
        public static readonly InSegmentOperator Instance = new();
        
        private InSegmentOperator() { }
        
        public string Name => "InSegment";
        
        public object Compile(string ruleValue) => ruleValue;
        public bool Evaluate(string userValue, object? compiledRuleValue) => false;
    }
}
