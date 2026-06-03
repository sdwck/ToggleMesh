using ToggleMesh.SDK.Contexts;

namespace ToggleMesh.SDK.Rules;

public interface IRuleEngine
{
    public bool Evaluate<TAccessor>(CompiledRuleGroup[] groups, ref EvaluationContext<TAccessor> context) where TAccessor : IContextAccessor;
    public CompiledRuleGroup[] CompileRules(IEnumerable<RuleDto>? rules);
}