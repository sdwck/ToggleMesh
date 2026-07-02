using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Organizations.Domain;

public class OrganizationInvitation : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    
    public string Email { get; set; } = string.Empty;
    public OrganizationRole Role { get; set; } = OrganizationRole.Member;
    
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
