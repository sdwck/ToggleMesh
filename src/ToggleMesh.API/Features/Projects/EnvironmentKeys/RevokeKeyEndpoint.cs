using Microsoft.EntityFrameworkCore;
using FastEndpoints;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Projects.EnvironmentKeys;

public class RevokeKeyEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly IApiKeyCacheService _apiKeyCache;

    public RevokeKeyEndpoint(AppDbContext db, IApiKeyCacheService apiKeyCache)
    {
        _db = db;
        _apiKeyCache = apiKeyCache;
    }

    public override void Configure()
    {
        Delete("/projects/{projectId:guid}/environments/{environmentId:guid}/keys/{keyId:guid}");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.EnvironmentsKeysRotate}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var environmentId = Route<Guid>("environmentId");
        var keyId = Route<Guid>("keyId");

        var env = await _db.Environments
            .FirstOrDefaultAsync(e => e.Id == environmentId && e.ProjectId == projectId, ct);

        if (env == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var key = await _db.EnvironmentKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.EnvironmentId == environmentId, ct);

        if (key == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        _db.EnvironmentKeys.Remove(key);
        await _db.SaveChangesAsync(ct);

        await _apiKeyCache.RemoveEnvironmentIdAsync(key.KeyHash, ct);

        await Send.NoContentAsync(ct);
    }
}
