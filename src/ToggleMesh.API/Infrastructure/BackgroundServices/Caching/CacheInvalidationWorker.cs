using StackExchange.Redis;

namespace ToggleMesh.API.Infrastructure.BackgroundServices.Caching;

public class CacheInvalidationWorker : BackgroundService
{
    private const string InvalidationChannel = "cache-invalidation:env";
    private static readonly RedisChannel InvalidationRedisChannel =
        RedisChannel.Literal(InvalidationChannel);
    private readonly IConnectionMultiplexer _redis;
    private readonly IEnumerable<ICacheInvalidationHandler> _cacheInvalidationHandlers;
    private readonly ILogger<CacheInvalidationWorker> _logger;

    public CacheInvalidationWorker(
        IConnectionMultiplexer redis,
        ILogger<CacheInvalidationWorker> logger, 
        IEnumerable<ICacheInvalidationHandler> cacheInvalidationHandlers)
    {
        _redis = redis;
        _logger = logger;
        _cacheInvalidationHandlers = cacheInvalidationHandlers;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = _redis.GetSubscriber();
        await sub.SubscribeAsync(InvalidationRedisChannel, (channel, message) =>
        {
            _ = Task.Run(async () =>
            {
                var msgStr = message.ToString();
                try
                {
                    var handler = _cacheInvalidationHandlers.FirstOrDefault(h => h.CanHandle(msgStr));
                    if (handler != null)
                        await handler.HandleAsync(msgStr, stoppingToken);
                    else
                        _logger.LogWarning("No cache invalidation handler registered for message: {Message}", msgStr);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during background cache invalidation for message {Message}", msgStr);
                }
            }, stoppingToken);
        });

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
