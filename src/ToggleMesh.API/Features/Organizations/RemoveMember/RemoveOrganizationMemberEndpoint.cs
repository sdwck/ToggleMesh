using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Organizations.RemoveMember;

public class RemoveOrganizationMemberEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public RemoveOrganizationMemberEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/organizations/{OrganizationId:guid}/members/{UserId:guid}");
        Version(1);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var organizationId = Route<Guid>("OrganizationId");
        var userId = Route<Guid>("UserId");

        var currentUserMember = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == UserId, ct);

        if (currentUserMember is not { Role: OrganizationRole.Admin })
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (UserId == userId)
        {
            AddError("You cannot remove yourself from the organization.");
            await Send.ErrorsAsync(400, cancellation: ct);
            return;
        }

        var memberToRemove = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);

        if (memberToRemove == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        _db.OrganizationMembers.Remove(memberToRemove);
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
