using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace ToggleMesh.API.Infrastructure.BackgroundServices.Caching.Handlers;

public class ApiKeyInvalidationHandler : ICacheInvalidationHandler
{
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;

    public ApiKeyInvalidationHandler(IConnectionMultiplexer redis, IMemoryCache memoryCache)
    {
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
    }

    public bool CanHandle(string message) => 
        message.StartsWith("apikey:", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(string message, CancellationToken ct)
    {
        _memoryCache.Remove(message);
        await _redis.KeyDeleteAsync(message);
    }
}