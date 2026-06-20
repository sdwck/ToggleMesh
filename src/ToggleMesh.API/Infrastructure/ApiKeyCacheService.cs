using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Infrastructure;

public class ApiKeyCacheService : IApiKeyCacheService
{
    private const string InvalidationChannel = "cache-invalidation:env";

    private static readonly RedisChannel InvalidationRedisChannel =
        RedisChannel.Literal(InvalidationChannel);

    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _memoryCache;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    public ApiKeyCacheService(
        AppDbContext db,
        IConnectionMultiplexer redis,
        IMemoryCache memoryCache)
    {
        _db = db;
        _redis = redis;
        _memoryCache = memoryCache;
    }

    public async Task<CachedKeyInfo?> GetKeyInfoAsync(string apiKey, CancellationToken ct = default)
    {
        var keyHash = ApiKeyHasher.Hash(apiKey);
        var cacheKey = $"apikey:{keyHash}";

        return await _memoryCache.GetOrCreateAsync<CachedKeyInfo?>(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheTtl;
            
            var db = _redis.GetDatabase();
            var redisValue = await db.StringGetAsync(cacheKey);
            if (redisValue.HasValue)
                return JsonSerializer.Deserialize<CachedKeyInfo>((string)redisValue!);
            

            var envKey = await _db.EnvironmentKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.KeyHash == keyHash && (x.ExpireOn == null || x.ExpireOn > DateTime.UtcNow), ct);

            if (envKey == null)
                return null;

            await _db.EnvironmentKeys
                .Where(x => x.Id == envKey.Id)
                .ExecuteUpdateAsync(s => 
                    s.SetProperty(k => 
                        k.LastUsedAt, DateTime.UtcNow), ct);

            var info = new CachedKeyInfo(envKey.EnvironmentId, envKey.KeyType);

            if (envKey.ExpireOn.HasValue)
            {
                var timeToExpire = envKey.ExpireOn.Value - DateTime.UtcNow;
                if (timeToExpire <= TimeSpan.Zero)
                    return null;

                if (timeToExpire < _cacheTtl)
                {
                    entry.AbsoluteExpirationRelativeToNow = timeToExpire;
                    await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(info), timeToExpire);
                }
                else
                    await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(info), _cacheTtl);
            }
            else
                await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(info), _cacheTtl);

            return info;
        });
    }

    public async Task RemoveEnvironmentIdAsync(string keyHash, CancellationToken ct = default)
    {
        var cacheKey = $"apikey:{keyHash}";
        _memoryCache.Remove(cacheKey);
        await _redis.GetDatabase().KeyDeleteAsync(cacheKey);

        var sub = _redis.GetSubscriber();
        await sub.PublishAsync(
            InvalidationRedisChannel,
            cacheKey);
    }

    public async Task SetEnvironmentIdAsync(string keyHash, Guid environmentId, bool isClient = false,
        CancellationToken ct = default)
    {
        var cacheKey = $"apikey:{keyHash}";
        var keyType = isClient ? KeyType.Client : KeyType.Server;
        var keyInfo = new CachedKeyInfo(environmentId, keyType);

        _memoryCache.Set(cacheKey, keyInfo, _cacheTtl);
        await _redis.GetDatabase().StringSetAsync(cacheKey, JsonSerializer.Serialize(keyInfo), _cacheTtl);
    }
}