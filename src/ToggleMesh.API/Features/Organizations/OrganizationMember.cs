using ToggleMesh.API.Features.Auth.Models;

namespace ToggleMesh.API.Features.Organizations;

public class OrganizationMember
{
    public Guid Id { get; set; }
    
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public OrganizationRole Role { get; set; } = OrganizationRole.Member;
}
