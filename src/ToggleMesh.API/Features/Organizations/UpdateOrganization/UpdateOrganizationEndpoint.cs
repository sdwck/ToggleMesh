using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Organizations.UpdateOrganization;

public class UpdateOrganizationEndpoint : ToggleEndpoint<UpdateOrganizationRequest>
{
    private readonly AppDbContext _db;

    public UpdateOrganizationEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/organizations/{OrganizationId:guid}");
        Version(1);
    }

    public override async Task HandleAsync(UpdateOrganizationRequest req, CancellationToken ct)
    {
        var organizationId = Route<Guid>("OrganizationId");

        var currentUserMember = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == UserId, ct);

        if (currentUserMember is not { Role: OrganizationRole.Admin })
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            AddError("Organization name is required.");
            await Send.ErrorsAsync(400, cancellation: ct);
            return;
        }

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        org.Name = req.Name.Trim();
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
