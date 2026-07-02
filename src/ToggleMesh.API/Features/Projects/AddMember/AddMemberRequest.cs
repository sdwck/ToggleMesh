using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Features.Projects.AddMember;

public class AddMemberRequest
{
    public string Email { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
}