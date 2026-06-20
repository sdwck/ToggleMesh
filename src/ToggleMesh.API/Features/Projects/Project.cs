using ToggleMesh.API.Features.Organizations;

using ToggleMesh.API.Persistence;
using ToggleMesh.API.Persistence.Abstractions;

namespace ToggleMesh.API.Features.Projects;

public class Project : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public ICollection<ProjectEnvironment> Environments { get; set; } = [];
    public ICollection<ProjectMember> Members { get; set; } = [];
}