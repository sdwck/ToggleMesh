using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Infrastructure;

public record CachedKeyInfo(Guid EnvironmentId, KeyType KeyType);

public interface IApiKeyCacheService
{
    Task<CachedKeyInfo?> GetKeyInfoAsync(string apiKey, CancellationToken ct = default);
    Task RemoveEnvironmentIdAsync(string keyHash, CancellationToken ct = default);
    Task SetEnvironmentIdAsync(string keyHash, Guid environmentId, bool isClient = false, CancellationToken ct = default);
}