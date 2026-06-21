namespace ToggleMesh.API.Features.Organizations.GetInvitation;

public class OrganizationInvitationDto
{
    public string OrganizationName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public OrganizationRole Role { get; set; }
}