using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Flags;

public class FeatureFlag : AuditableEntity, ISoftDeletable
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public string Key { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    
    public bool IsDeleted { get; set; }

    public ICollection<FlagEnvironmentState> States { get; set; } = new List<FlagEnvironmentState>();
    public bool IsClientSideExposed { get; set; }
    public string[] Tags { get; set; } = [];
}
