using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Projects.UpdateEnvironment;

public class UpdateEnvironmentEndpoint : ToggleEndpoint<UpdateEnvironmentRequest>
{
    private readonly AppDbContext _db;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateEnvironmentEndpoint(AppDbContext db, ICacheInvalidator cacheInvalidator)
    {
        _db = db;
        _cacheInvalidator = cacheInvalidator;
    }

    public override void Configure()
    {
        Put("/projects/{projectId:guid}/environments/{environmentId:guid}");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.EnvironmentsEdit);
    }

    public override async Task HandleAsync(UpdateEnvironmentRequest req, CancellationToken ct)
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

        env.Name = req.Name.Trim();
        await _db.SaveChangesAsync(ct);

        await _cacheInvalidator.InvalidateEnvironmentCacheAsync(environmentId);

        await Send.NoContentAsync(ct);
    }
}