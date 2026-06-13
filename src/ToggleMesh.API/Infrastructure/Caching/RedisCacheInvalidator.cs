using Microsoft.Extensions.Caching.Hybrid;
using StackExchange.Redis;

namespace ToggleMesh.API.Infrastructure.Caching;

public class RedisCacheInvalidator : ICacheInvalidator
{
    private const string InvalidationChannel = "cache-invalidation:env";
    private static readonly RedisChannel InvalidationRedisChannel =
        RedisChannel.Literal(InvalidationChannel);
    private readonly IConnectionMultiplexer _redis;
    private readonly HybridCache _cache;

    public RedisCacheInvalidator(IConnectionMultiplexer redis, HybridCache cache)
    {
        _redis = redis;
        _cache = cache;
    }
    
    public async Task InvalidateEnvironmentCacheAsync(Guid envId)
    {
        var l1CacheKey = $"sdk:compiled_rules:{envId}";
        var l2CacheKey = $"sdk:flags:states:{envId}";
        await _cache.RemoveAsync(l1CacheKey);
        await _cache.RemoveAsync(l2CacheKey);
        var sub = _redis.GetSubscriber();
        await sub.PublishAsync(
            InvalidationRedisChannel, 
            envId.ToString());
    }
}