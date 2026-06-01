using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Projects;

public interface IApiKeyCacheService
{
    Task<Guid?> GetEnvironmentIdAsync(string apiKey, CancellationToken ct = default);
}

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
        var cacheKey = $"apikey:{apiKey}";

        var cachedValue = await db.StringGetAsync(cacheKey);
        
        if (cachedValue.HasValue)
        {
            if (cachedValue.ToString() == "invalid")
            {
                return null;
            }
            
            if (Guid.TryParse(cachedValue.ToString(), out var envId))
            {
                return envId;
            }
        }

        var envKey = await _db.EnvironmentKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApiKey == apiKey && (x.ExpireOn == null || x.ExpireOn > DateTime.UtcNow), ct);

        if (envKey == null)
        {
            await db.StringSetAsync(cacheKey, "invalid", _cacheTtl);
            return null;
        }

        var ttl = _cacheTtl;
        if (envKey.ExpireOn.HasValue)
        {
            var timeToExpire = envKey.ExpireOn.Value - DateTime.UtcNow;
            if (timeToExpire < _cacheTtl)
            {
                ttl = timeToExpire;
            }
        }

        await db.StringSetAsync(cacheKey, envKey.EnvironmentId.ToString(), ttl);
        return envKey.EnvironmentId;
    }
}
