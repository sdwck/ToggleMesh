using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Flags.Domain;

public class FlagIndividualTarget : AuditableEntity
{
    public Guid FlagEnvironmentStateId { get; set; }
    public FlagEnvironmentState FlagEnvironmentState { get; set; } = null!;
    
    public string IdentityKey { get; set; } = string.Empty;
    public Guid VariationId { get; set; }
    public FlagVariation Variation { get; set; } = null!;
}
