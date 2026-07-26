using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Projects.ClearEnvironmentPiiAlert;

public class ClearEnvironmentPiiAlertEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public ClearEnvironmentPiiAlertEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/environments/{environmentId:guid}/clear-pii-alert");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.EnvironmentsEdit);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var environmentId = Route<Guid>("environmentId");

        var env = await _db.Environments
            .FirstOrDefaultAsync(e => e.Id == environmentId && e.ProjectId == projectId, ct);

        if (env == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        env.LastPiiBlockedContext = null;
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
