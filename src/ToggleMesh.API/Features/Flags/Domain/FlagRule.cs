using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Flags.Domain;

public class FlagRule : AuditableEntity
{
    public Guid FlagEnvironmentStateId { get; set; }
    public FlagEnvironmentState FlagEnvironmentState { get; set; } = null!;
    public int GroupId { get; set; } 
    public string Attribute { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}