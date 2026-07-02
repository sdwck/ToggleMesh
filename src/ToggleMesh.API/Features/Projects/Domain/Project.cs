using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Projects.Domain;

public class Project : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public ICollection<ProjectEnvironment> Environments { get; set; } = [];
    public ICollection<ProjectMember> Members { get; set; } = [];
}