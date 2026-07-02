using ToggleMesh.API.Features.Organizations.Domain;

namespace ToggleMesh.API.Features.Organizations.InviteMember;

public class InviteMemberRequest
{
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = string.Empty;
    public OrganizationRole Role { get; set; } = OrganizationRole.Member;
}