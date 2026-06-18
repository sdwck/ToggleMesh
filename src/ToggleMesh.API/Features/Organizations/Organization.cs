using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Features.Organizations;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<OrganizationMember> Members { get; set; } = [];
}
