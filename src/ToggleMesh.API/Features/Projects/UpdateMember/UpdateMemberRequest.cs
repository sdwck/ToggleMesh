using ToggleMesh.API.Features.Projects.GetMembers;

namespace ToggleMesh.API.Features.Projects.UpdateMember;

public class UpdateMemberRequest
{
    public ProjectRole Role { get; set; }
    public List<EnvironmentRoleDto>? EnvironmentRoles { get; set; }
}
