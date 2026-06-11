using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Flags.UpdatePrivacy;

public class UpdateFlagPrivacyEndpoint : ToggleEndpoint<UpdateFlagPrivacyRequest>
{
    private readonly AppDbContext _db;

    public UpdateFlagPrivacyEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Patch("/projects/{projectId:guid}/flags/{flagKey}/privacy");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.FlagsEdit}");
    }

    public override async Task HandleAsync(UpdateFlagPrivacyRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var flagKey = Route<string>("flagKey");

        var flag = await _db.FeatureFlags
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Key == flagKey, ct);

        if (flag == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        flag.IsClientSideExposed = req.IsClientSideExposed;
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}