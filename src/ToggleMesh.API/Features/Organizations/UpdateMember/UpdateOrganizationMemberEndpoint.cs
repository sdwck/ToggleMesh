using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Organizations.UpdateMember;

public record UpdateOrganizationMemberRequest(OrganizationRole Role);

public class UpdateOrganizationMemberEndpoint : ToggleEndpoint<UpdateOrganizationMemberRequest>
{
    private readonly AppDbContext _db;

    public UpdateOrganizationMemberEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/organizations/{OrganizationId:guid}/members/{UserId:guid}");
        Version(1);
    }

    public override async Task HandleAsync(UpdateOrganizationMemberRequest req, CancellationToken ct)
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
            AddError("You cannot change your own organization role.");
            await Send.ErrorsAsync(400, cancellation: ct);
            return;
        }

        var memberToUpdate = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);

        if (memberToUpdate == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        memberToUpdate.Role = req.Role;
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
