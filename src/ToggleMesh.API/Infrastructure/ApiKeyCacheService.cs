using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
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
    private readonly HybridCache _cache;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    public ApiKeyCacheService(
        AppDbContext db,
        IConnectionMultiplexer redis,
        HybridCache cache)
    {
        _db = db;
        _redis = redis;
        _cache = cache;
    }

    public async Task<CachedKeyInfo?> GetKeyInfoAsync(string apiKey, CancellationToken ct = default)
    {
        var keyHash = ApiKeyHasher.Hash(apiKey);
        var cacheKey = $"apikey:{keyHash}";

        return await _cache.GetOrCreateAsync<CachedKeyInfo?>(
            cacheKey,
            async ct1 =>
            {
                var envKey = await _db.EnvironmentKeys
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.KeyHash == keyHash && (x.ExpireOn == null || x.ExpireOn > DateTime.UtcNow), ct1);

                if (envKey == null)
                    return null;

                var info = new CachedKeyInfo(envKey.EnvironmentId, envKey.KeyType);

                if (envKey.ExpireOn.HasValue)
                {
                    var timeToExpire = envKey.ExpireOn.Value - DateTime.UtcNow;
                    if (timeToExpire <= TimeSpan.Zero)
                        return null;

                    if (timeToExpire < _cacheTtl)
                        await _cache.SetAsync(cacheKey, info, new HybridCacheEntryOptions { Expiration = timeToExpire },
                            cancellationToken: ct1);
                }

                return info;
            },
            cancellationToken: ct);
    }

    public async Task RemoveEnvironmentIdAsync(string keyHash, CancellationToken ct = default)
    {
        var cacheKey = $"apikey:{keyHash}";
        await _cache.RemoveAsync(cacheKey, ct);

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

        await _cache.SetAsync(
            cacheKey, 
            keyInfo, 
            new HybridCacheEntryOptions { Expiration = _cacheTtl }, 
            cancellationToken: ct);
    }
}