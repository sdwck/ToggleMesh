using ToggleMesh.Common.Contexts;

namespace ToggleMesh.Common.Rules;

public interface IRuleEngine
{
    public bool Evaluate<TAccessor>(CompiledRuleGroup[] groups, ref EvaluationContext<TAccessor> context) where TAccessor : IContextAccessor;
    public CompiledRuleGroup[] CompileRules(IEnumerable<RuleDto>? rules);
}