namespace ToggleMesh.API.Features.Flags;

public class FlagRule
{
    public Guid Id { get; set; }
    public Guid FlagEnvironmentStateId { get; set; }
    public FlagEnvironmentState FlagEnvironmentState { get; set; } = null!;
    public int GroupId { get; set; } 
    public string Attribute { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}