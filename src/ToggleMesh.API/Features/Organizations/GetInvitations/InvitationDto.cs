using ToggleMesh.API.Features.Organizations.Domain;

namespace ToggleMesh.API.Features.Organizations.GetInvitations;

public class InvitationDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public OrganizationRole Role { get; set; }
    public DateTimeOffset InvitedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string Token { get; set; } = string.Empty;
}
