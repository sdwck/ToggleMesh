using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace ToggleMesh.API.Infrastructure.Caching;

public class RedisCacheInvalidator : ICacheInvalidator
{
    private const string InvalidationChannel = "cache-invalidation:env";
    private static readonly RedisChannel InvalidationRedisChannel =
        RedisChannel.Literal(InvalidationChannel);
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _memoryCache;

    public RedisCacheInvalidator(IConnectionMultiplexer redis, IMemoryCache memoryCache)
    {
        _redis = redis;
        _memoryCache = memoryCache;
    }
    
    public async Task InvalidateEnvironmentCacheAsync(Guid envId)
    {
        var l1CacheKey = CacheKeys.SdkCompiledRules(envId);
        var l2CacheKey = CacheKeys.SdkFlagsStates(envId);
        _memoryCache.Remove(l1CacheKey);
        await _redis.GetDatabase().KeyDeleteAsync(l1CacheKey);
        await _redis.GetDatabase().KeyDeleteAsync(l2CacheKey);
        var sub = _redis.GetSubscriber();
        await sub.PublishAsync(
            InvalidationRedisChannel, 
            envId.ToString());
    }
}