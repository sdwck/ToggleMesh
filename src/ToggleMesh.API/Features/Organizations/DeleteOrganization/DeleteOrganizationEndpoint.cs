using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Organizations.DeleteOrganization;

public class DeleteOrganizationEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public DeleteOrganizationEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/organizations/{OrganizationId:guid}");
        Version(1);
        PreProcessor<RequireOrgAdminPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var organizationId = Route<Guid>("OrganizationId");

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        _db.Organizations.Remove(org);
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
