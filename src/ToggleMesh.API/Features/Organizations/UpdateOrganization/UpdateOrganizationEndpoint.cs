using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

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
        PreProcessor<RequireOrgAdminPreProcessor<UpdateOrganizationRequest>>();
    }

    public override async Task HandleAsync(UpdateOrganizationRequest req, CancellationToken ct)
    {
        var organizationId = Route<Guid>("OrganizationId");
        
        if (string.IsNullOrWhiteSpace(req.Name))
            ThrowError("Organization name is required.", 400);

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
