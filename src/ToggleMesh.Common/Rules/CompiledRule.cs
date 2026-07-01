namespace ToggleMesh.Common.Rules;

public readonly struct CompiledRule : IEquatable<CompiledRule>
{
    public string Attribute { get; }
    public IRuleOperator Operator { get; }
    public object? CompiledValue { get; }

    public CompiledRule(string attribute, IRuleOperator op, object? compiledValue)
    {
        Attribute = attribute;
        Operator = op;
        CompiledValue = compiledValue;
    }

    public bool Equals(CompiledRule other)
    {
        return Attribute == other.Attribute &&
               EqualityComparer<IRuleOperator>.Default.Equals(Operator, other.Operator) &&
               EqualityComparer<object?>.Default.Equals(CompiledValue, other.CompiledValue);
    }

    public override bool Equals(object? obj)
    {
        return obj is CompiledRule other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Attribute, Operator, CompiledValue);
    }

    public static bool operator ==(CompiledRule left, CompiledRule right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CompiledRule left, CompiledRule right)
    {
        return !left.Equals(right);
    }
}