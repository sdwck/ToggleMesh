using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

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
        PreProcessor<RequireOrgAdminPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var organizationId = Route<Guid>("OrganizationId");

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
