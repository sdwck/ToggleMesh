namespace ToggleMesh.API.Features.Organizations.GetMembers;

public class OrganizationMemberDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public OrganizationRole Role { get; set; }
}