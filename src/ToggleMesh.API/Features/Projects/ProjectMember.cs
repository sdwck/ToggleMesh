using ToggleMesh.API.Features.Auth.Models;

namespace ToggleMesh.API.Features.Projects;

public class ProjectMember
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public ProjectRole Role { get; set; }
}