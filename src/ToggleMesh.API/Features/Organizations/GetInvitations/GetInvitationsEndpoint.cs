using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Organizations.GetInvitations;

public class GetInvitationsEndpoint : ToggleEndpointWithoutRequest<List<InvitationDto>>
{
    private readonly AppDbContext _db;

    public GetInvitationsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/organizations/{OrganizationId:guid}/invites");
        Version(1);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var organizationId = Route<Guid>("OrganizationId");

        var currentUserMember = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == UserId, ct);

        if (currentUserMember is not { Role: OrganizationRole.Admin })
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var invites = await _db.OrganizationInvitations
            .Where(i => i.OrganizationId == organizationId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationDto
            {
                Id = i.Id,
                Email = i.Email,
                Role = i.Role,
                InvitedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt,
                Token = i.Token
            })
            .ToListAsync(ct);

        await Send.OkAsync(invites, cancellation: ct);
    }
}
