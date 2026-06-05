using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using FastEndpoints;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Security;

namespace ToggleMesh.API.Features.Projects.RotateEnvironmentKey;

public class RotateEnvironmentKeyEndpoint : ToggleEndpointWithoutRequest<RotateEnvironmentKeyResponse>
{
    private readonly AppDbContext _db;
    private readonly IApiKeyCacheService _apiKeyCache;

    public RotateEnvironmentKeyEndpoint(AppDbContext db, IApiKeyCacheService apiKeyCache)
    {
        _db = db;
        _apiKeyCache = apiKeyCache;
    }

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/environments/{environmentId:guid}/keys/rotate");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.EnvironmentsKeysRotate}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var environmentId = Route<Guid>("environmentId");
        
        var env = await _db.Environments
            .Include(e => e.Keys)
            .FirstOrDefaultAsync(e => e.Id == environmentId && e.ProjectId == projectId, ct);

        if (env == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var rawSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "").Replace("/", "").Replace("=", "");
        var plainKey = $"tm_{rawSecret}";
        
        var keyHash = ApiKeyHasher.Hash(plainKey);
        var keyPreview = ApiKeyHasher.GeneratePreview(plainKey);
        
        foreach (var oldKey in env.Keys)
        {
            oldKey.ExpireOn = DateTime.UtcNow;
            await _apiKeyCache.RemoveEnvironmentIdAsync(oldKey.KeyHash, ct);
        }

        var newKey = new EnvironmentKey
        {
            EnvironmentId = env.Id,
            KeyHash = keyHash,
            KeyPreview = keyPreview,
            CreatedOn = DateTime.UtcNow
        };

        _db.EnvironmentKeys.Add(newKey);
        await _db.SaveChangesAsync(ct);

        await _apiKeyCache.SetEnvironmentIdAsync(keyHash, env.Id, ct);

        await Send.OkAsync(new RotateEnvironmentKeyResponse { ApiKey = plainKey }, ct);
    }
}