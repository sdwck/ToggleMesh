using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Infrastructure;

public record CachedKeyInfo(Guid EnvironmentId, KeyType KeyType);

public interface IApiKeyCacheService
{
    Task<CachedKeyInfo?> GetKeyInfoAsync(string apiKey, CancellationToken ct = default);
    Task RemoveEnvironmentIdAsync(string apiKey, CancellationToken ct = default);
    Task SetEnvironmentIdAsync(string apiKey, Guid environmentId, bool isClient = false, CancellationToken ct = default);
}