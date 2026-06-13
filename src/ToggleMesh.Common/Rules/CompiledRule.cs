namespace ToggleMesh.Common.Rules;

public readonly struct CompiledRule
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
}