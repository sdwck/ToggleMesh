namespace ToggleMesh.Common.Rules;

public readonly struct CompiledRuleGroup : IEquatable<CompiledRuleGroup>
{
    public CompiledRule[] Rules { get; }
    public VariationWeight[]? Rollout { get; }
    public Guid? FastResultVariationId { get; }

    public CompiledRuleGroup(CompiledRule[] rules, VariationWeight[]? rollout = null)
    {
        Rules = rules;
        Rollout = rollout;
        
        if (rollout is [{ Weight: >= 10000 }])
            FastResultVariationId = rollout[0].VariationId;
    }

    public bool Equals(CompiledRuleGroup other)
    {
        return Rules == other.Rules && Rollout == other.Rollout;
    }

    public override bool Equals(object? obj)
    {
        return obj is CompiledRuleGroup other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Rules?.GetHashCode() ?? 0;
    }

    public static bool operator ==(CompiledRuleGroup left, CompiledRuleGroup right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CompiledRuleGroup left, CompiledRuleGroup right)
    {
        return !left.Equals(right);
    }
}
