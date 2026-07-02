using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Organizations.Domain;

public class Organization : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<OrganizationMember> Members { get; set; } = [];
    public bool IsDeleted { get; set; }
}
