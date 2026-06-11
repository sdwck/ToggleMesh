namespace ToggleMesh.Common.Rules;

public readonly struct CompiledRule
{
    public string Attribute { get; }
    public IRuleOperator Operator { get; }
    public string Value { get; }

    public CompiledRule(string attribute, IRuleOperator op, string value)
    {
        Attribute = attribute;
        Operator = op;
        Value = value;
    }
}