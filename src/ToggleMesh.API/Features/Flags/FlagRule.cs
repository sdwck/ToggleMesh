namespace ToggleMesh.API.Features.Flags;

public class FlagRule
{
    public int Id { get; set; }
    public int FeatureFlagId { get; set; }
    public string Attribute { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}