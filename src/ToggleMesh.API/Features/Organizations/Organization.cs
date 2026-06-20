using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Persistence.Abstractions;

namespace ToggleMesh.API.Features.Organizations;

public class Organization : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<OrganizationMember> Members { get; set; } = [];
    public bool IsDeleted { get; set; }
}
