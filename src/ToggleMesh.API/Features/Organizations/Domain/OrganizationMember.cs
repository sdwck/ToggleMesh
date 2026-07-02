using ToggleMesh.API.Infrastructure.Data.Abstractions;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Organizations.Domain;

public class OrganizationMember : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public OrganizationRole Role { get; set; } = OrganizationRole.Member;
}
