using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Organizations.RevokeInvitation;

public class RevokeInvitationEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public RevokeInvitationEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/organizations/{OrganizationId:guid}/invites/{InviteId:guid}");
        Version(1);
        PreProcessor<RequireOrgAdminPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var organizationId = Route<Guid>("OrganizationId");
        var inviteId = Route<Guid>("InviteId");
        
        var invite = await _db.OrganizationInvitations
            .FirstOrDefaultAsync(i => i.OrganizationId == organizationId && i.Id == inviteId, ct);

        if (invite == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        _db.OrganizationInvitations.Remove(invite);
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
