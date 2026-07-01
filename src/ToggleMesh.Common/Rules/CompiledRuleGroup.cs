namespace ToggleMesh.Common.Rules;

public readonly struct CompiledRuleGroup : IEquatable<CompiledRuleGroup>
{
    public CompiledRule[] Rules { get; }

    public CompiledRuleGroup(CompiledRule[] rules)
    {
        Rules = rules;
    }

    public bool Equals(CompiledRuleGroup other)
    {
        return Rules == other.Rules;
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