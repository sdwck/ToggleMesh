using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Infrastructure;

public class ApiKeyCacheService : IApiKeyCacheService
{
    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    public ApiKeyCacheService(AppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis;
    }

    public async Task<CachedKeyInfo?> GetKeyInfoAsync(string apiKey, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var keyHash = ApiKeyHasher.Hash(apiKey);
        var cacheKey = $"apikey:{keyHash}";

        var cachedValue = await db.StringGetAsync(cacheKey);

        if (cachedValue.HasValue)
        {
            var value = cachedValue.ToString();
            if (value == "invalid")
                return null;

            var parts = value.Split(':');
            if (parts.Length == 2 && Guid.TryParse(parts[0], out var envId) && Enum.TryParse<KeyType>(parts[1], out var keyType))
            {
                return new CachedKeyInfo(envId, keyType);
            }
        }

        var envKey = await _db.EnvironmentKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.KeyHash == keyHash && (x.ExpireOn == null || x.ExpireOn > DateTime.UtcNow), ct);

        if (envKey == null)
        {
            await db.StringSetAsync(cacheKey, "invalid", _cacheTtl);
            return null;
        }

        var ttl = _cacheTtl;
        if (envKey.ExpireOn.HasValue)
        {
            var timeToExpire = envKey.ExpireOn.Value - DateTime.UtcNow;
            if (timeToExpire <= TimeSpan.Zero)
            {
                await db.StringSetAsync(cacheKey, "invalid", _cacheTtl);
                return null;
            }
            if (timeToExpire < _cacheTtl)
                ttl = timeToExpire;
        }

        var newValue = $"{envKey.EnvironmentId}:{(int)envKey.KeyType}";
        await db.StringSetAsync(cacheKey, newValue, ttl);
        
        return new CachedKeyInfo(envKey.EnvironmentId, envKey.KeyType);
    }

    public async Task RemoveEnvironmentIdAsync(string apiKey, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var cacheKey = $"apikey:{apiKey}";
        await db.KeyDeleteAsync(cacheKey);
    }

    public async Task SetEnvironmentIdAsync(string apiKey, Guid environmentId, bool isClient = false, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var cacheKey = $"apikey:{apiKey}";
        var keyType = isClient ? KeyType.Client : KeyType.Server;
        var newValue = $"{environmentId}:{(int)keyType}";
        
        await db.StringSetAsync(cacheKey, newValue, _cacheTtl);
    }
}
