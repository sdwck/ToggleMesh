using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Projects;

public class MemberEnvironmentRole : IHasEnvironment
{
    public Guid Id { get; set; }
    
    public Guid ProjectMemberId { get; set; }
    public ProjectMember ProjectMember { get; set; } = null!;
    
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
    
    public ProjectRole Role { get; set; }
}
