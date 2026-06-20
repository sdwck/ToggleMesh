using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Persistence.Abstractions;

namespace ToggleMesh.API.Features.Projects;

public class ProjectMember : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public ProjectRole Role { get; set; }
    
    public ICollection<MemberEnvironmentRole> EnvironmentRoles { get; set; } = new List<MemberEnvironmentRole>();
}