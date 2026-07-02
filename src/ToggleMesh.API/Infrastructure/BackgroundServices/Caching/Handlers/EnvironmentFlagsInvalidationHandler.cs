using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Infrastructure.Caching;

namespace ToggleMesh.API.Infrastructure.BackgroundServices.Caching.Handlers;

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
            var l1CacheKey = CacheKeys.SdkCompiledRules(envId);
            var l2CacheKey = CacheKeys.SdkFlagsStates(envId);

            _memoryCache.Remove(l1CacheKey);
            await _redis.KeyDeleteAsync(l1CacheKey);
            await _redis.KeyDeleteAsync(l2CacheKey);
        }
    }
}