using Microsoft.EntityFrameworkCore;
using FastEndpoints;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Caching;

namespace ToggleMesh.API.Features.Projects.DeleteEnvironment;

public class DeleteEnvironmentEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly IApiKeyCacheService _apiKeyCache;

    public DeleteEnvironmentEndpoint(
        AppDbContext db, 
        ICacheInvalidator cacheInvalidator,
        IApiKeyCacheService apiKeyCache)
    {
        _db = db;
        _cacheInvalidator = cacheInvalidator;
        _apiKeyCache = apiKeyCache;
    }

    public override void Configure()
    {
        Delete("/projects/{projectId:guid}/environments/{environmentId:guid}");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.EnvironmentsDelete);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var environmentId = Route<Guid>("environmentId");
        
        var envCount = await _db.Environments
            .CountAsync(e => e.ProjectId == projectId, ct);

        if (envCount <= 1)
        {
            AddError("A project must have at least one environment. You cannot delete the last one.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var env = await _db.Environments
            .Include(e => e.Keys)
            .FirstOrDefaultAsync(e => e.Id == environmentId && e.ProjectId == projectId, ct);

        if (env == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        foreach (var key in env.Keys)
            await _apiKeyCache.RemoveEnvironmentIdAsync(key.KeyHash, ct);

        _db.Environments.Remove(env);
        await _db.SaveChangesAsync(ct);

        await _cacheInvalidator.InvalidateEnvironmentCacheAsync(environmentId);

        await Send.NoContentAsync(ct);
    }
}