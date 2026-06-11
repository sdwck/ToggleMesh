using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace ToggleMesh.API.BackgroundServices;

public class CacheInvalidationWorker : BackgroundService
{
    private const string InvalidationChannel = "cache-invalidation:env";
    private static readonly RedisChannel InvalidationRedisChannel =
        RedisChannel.Literal(InvalidationChannel);
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _memoryCache;

    public CacheInvalidationWorker(
        IConnectionMultiplexer redis, 
        IMemoryCache memoryCache)
    {
        _redis = redis;
        _memoryCache = memoryCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = _redis.GetSubscriber();
        await sub.SubscribeAsync(InvalidationRedisChannel, (channel, message) =>
        {
            if (!Guid.TryParse(message.ToString(), out var envId)) 
                return;
            
            var cacheKey = $"sdk:compiled_rules:{envId}";
            _memoryCache.Remove(cacheKey);
        });
        
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}