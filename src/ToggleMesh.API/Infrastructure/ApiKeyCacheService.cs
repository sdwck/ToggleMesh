using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
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

    public async Task<Guid?> GetEnvironmentIdAsync(string apiKey, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var keyHash = ApiKeyHasher.Hash(apiKey);
        var cacheKey = $"apikey:{keyHash}";

        var cachedValue = await db.StringGetAsync(cacheKey);

        if (cachedValue.HasValue)
        {
            if (cachedValue.ToString() == "invalid")
                return null;

            if (Guid.TryParse(cachedValue.ToString(), out var envId))
                return envId;
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

        await db.StringSetAsync(cacheKey, envKey.EnvironmentId.ToString(), ttl);
        return envKey.EnvironmentId;
    }

    public async Task RemoveEnvironmentIdAsync(string apiKey, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var cacheKey = $"apikey:{apiKey}";
        await db.KeyDeleteAsync(cacheKey);
    }

    public async Task SetEnvironmentIdAsync(string apiKey, Guid environmentId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var cacheKey = $"apikey:{apiKey}";
        await db.StringSetAsync(cacheKey, environmentId.ToString(), _cacheTtl);
    }
}
