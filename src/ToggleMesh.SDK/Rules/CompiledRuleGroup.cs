namespace ToggleMesh.SDK.Rules;

public readonly struct CompiledRuleGroup
{
    public CompiledRule[] Rules { get; }

    public CompiledRuleGroup(CompiledRule[] rules)
    {
        Rules = rules;
    }
}