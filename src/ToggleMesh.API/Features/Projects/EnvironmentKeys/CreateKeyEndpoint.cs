using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using ToggleMesh.API.Persistence;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Security;

namespace ToggleMesh.API.Features.Projects.EnvironmentKeys;

public class CreateKeyEndpoint : ToggleEndpoint<CreateKeyRequest, CreateKeyResponse>
{
    private readonly AppDbContext _db;
    private readonly IApiKeyCacheService _apiKeyCache;

    public CreateKeyEndpoint(AppDbContext db, IApiKeyCacheService apiKeyCache)
    {
        _db = db;
        _apiKeyCache = apiKeyCache;
    }

    public override void Configure()
    {
        Post("/projects/{projectId:guid}/environments/{environmentId:guid}/keys");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.EnvironmentsKeysRotate}");
    }

    public override async Task HandleAsync(CreateKeyRequest req, CancellationToken ct)
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

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            AddError("Key name is required.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var rawSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");

        var plainKey = req.Type == KeyType.Client 
            ? $"tm_client_{rawSecret}" 
            : $"tm_server_{rawSecret}";

        var keyHash = ApiKeyHasher.Hash(plainKey);
        var keyPreview = ApiKeyHasher.GeneratePreview(plainKey);

        var newKey = new EnvironmentKey
        {
            Id = Guid.CreateVersion7(),
            Name = req.Name,
            EnvironmentId = env.Id,
            KeyHash = keyHash,
            KeyPreview = keyPreview,
            CreatedOn = DateTime.UtcNow,
            KeyType = req.Type
        };

        _db.EnvironmentKeys.Add(newKey);
        await _db.SaveChangesAsync(ct);

        await _apiKeyCache.SetEnvironmentIdAsync(keyHash, env.Id, req.Type == KeyType.Client, ct);

        await Send.OkAsync(new CreateKeyResponse
        {
            Id = newKey.Id,
            Name = newKey.Name,
            KeyType = newKey.KeyType,
            KeyPreview = newKey.KeyPreview,
            CreatedOn = newKey.CreatedOn,
            PlainKey = plainKey
        }, ct);
    }
}
