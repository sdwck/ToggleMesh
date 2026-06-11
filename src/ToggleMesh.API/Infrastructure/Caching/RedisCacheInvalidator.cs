using StackExchange.Redis;

namespace ToggleMesh.API.Infrastructure.Caching;

public class RedisCacheInvalidator : ICacheInvalidator
{
    private const string InvalidationChannel = "cache-invalidation:env";
    private static readonly RedisChannel InvalidationRedisChannel =
        RedisChannel.Literal(InvalidationChannel);
    private readonly IConnectionMultiplexer _redis;

    public RedisCacheInvalidator(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }
    
    public async Task InvalidateEnvironmentCacheAsync(Guid envId)
    {
        var db = _redis.GetDatabase();
        var sub = _redis.GetSubscriber();

        await db.KeyDeleteAsync($"sdk:flags:states:{envId}");
        await sub.PublishAsync(
            InvalidationRedisChannel, 
            envId.ToString());
    }
}