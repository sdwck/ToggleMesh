namespace ToggleMesh.API.Features.Projects.GetMembers;

public class MemberDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
    public List<EnvironmentRoleDto> EnvironmentRoles { get; set; } = new();
}

public class EnvironmentRoleDto
{
    public Guid EnvironmentId { get; set; }
    public ProjectRole Role { get; set; }
}