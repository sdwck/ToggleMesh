using StackExchange.Redis;
using Microsoft.Extensions.Caching.Memory;

namespace ToggleMesh.API.BackgroundServices.Caching.Handlers;

public class EnvironmentFlagsInvalidationHandler : ICacheInvalidationHandler
{
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;

    public EnvironmentFlagsInvalidationHandler(IConnectionMultiplexer redis, IMemoryCache memoryCache)
    {
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
    }

    public bool CanHandle(string message) => 
        Guid.TryParse(message, out _);

    public async Task HandleAsync(string message, CancellationToken ct)
    {
        if (Guid.TryParse(message, out var envId))
        {
            var l1CacheKey = $"sdk:compiled_rules:{envId}";
            var l2CacheKey = $"sdk:flags:states:{envId}";

            _memoryCache.Remove(l1CacheKey);
            await _redis.KeyDeleteAsync(l2CacheKey);
        }
    }
}