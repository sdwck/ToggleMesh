using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.UpdateMetadata;

public class UpdateFlagMetadataEndpoint : ToggleEndpoint<UpdateFlagMetadataRequest>
{
    private readonly AppDbContext _db;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateFlagMetadataEndpoint(AppDbContext db, ICacheInvalidator cacheInvalidator)
    {
        _db = db;
        _cacheInvalidator = cacheInvalidator;
    }

    public override void Configure()
    {
        Put("/projects/{projectId:guid}/flags/{key}/metadata");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(UpdateFlagMetadataRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var key = Route<string>("key");

        var flag = await _db.FeatureFlags
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Key == key, ct);

        if (flag == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        flag.Name = req.Name.Trim();
        flag.Description = req.Description.Trim();
        flag.Tags = req.Tags;

        await _db.SaveChangesAsync(ct);

        var envIds = await _db.Environments
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Select(e => e.Id)
            .ToListAsync(ct);

        foreach (var envId in envIds)
            await _cacheInvalidator.InvalidateEnvironmentCacheAsync(envId);

        await Send.NoContentAsync(ct);
    }
}