using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Features.Projects.GetMembers;

public class EnvironmentRoleDto
{
    public Guid EnvironmentId { get; set; }
    public ProjectRole Role { get; set; }
}