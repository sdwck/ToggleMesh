using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Flags.Domain;

public class FeatureFlag : AuditableEntity, ISoftDeletable
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public string Key { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    
    public bool IsDeleted { get; set; }

    public FlagType Type { get; set; } = FlagType.Boolean;
    public ICollection<FlagVariation> Variations { get; set; } = new List<FlagVariation>();

    public ICollection<FlagEnvironmentState> States { get; set; } = new List<FlagEnvironmentState>();
    public bool IsClientSideExposed { get; set; }
    public string[] Tags { get; set; } = [];
}
