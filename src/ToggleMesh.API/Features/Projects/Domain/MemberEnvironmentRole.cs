using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Projects.Domain;

public class MemberEnvironmentRole : AuditableEntity
{
    public Guid ProjectMemberId { get; set; }
    public ProjectMember ProjectMember { get; set; } = null!;
    
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
    
    public ProjectRole Role { get; set; }
}
