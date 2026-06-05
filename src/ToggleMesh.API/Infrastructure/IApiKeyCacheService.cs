namespace ToggleMesh.API.Infrastructure;

public interface IApiKeyCacheService
{
    Task<Guid?> GetEnvironmentIdAsync(string apiKey, CancellationToken ct = default);
    Task RemoveEnvironmentIdAsync(string apiKey, CancellationToken ct = default);
    Task SetEnvironmentIdAsync(string apiKey, Guid environmentId, CancellationToken ct = default);
}