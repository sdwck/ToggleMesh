using Microsoft.Extensions.Caching.Hybrid;

namespace ToggleMesh.API.BackgroundServices.Caching.Handlers;

public class EnvironmentFlagsInvalidationHandler : ICacheInvalidationHandler
{
    private readonly HybridCache _cache;

    public EnvironmentFlagsInvalidationHandler(HybridCache cache)
    {
        _cache = cache;
    }

    public bool CanHandle(string message) => 
        Guid.TryParse(message, out _);

    public async Task HandleAsync(string message, CancellationToken ct)
    {
        if (Guid.TryParse(message, out var envId))
        {
            var l1CacheKey = $"sdk:compiled_rules:{envId}";
            var l2CacheKey = $"sdk:flags:states:{envId}";

            await _cache.RemoveAsync(l1CacheKey, ct);
            await _cache.RemoveAsync(l2CacheKey, ct);
        }
    }
}