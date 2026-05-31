using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Get;

public class GetFlagEndpoint : Endpoint<GetFlagRequest, GetFlagResponse>
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;

    public GetFlagEndpoint(AppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis.GetDatabase();
    }

    public override void Configure()
    {
        Get("/api/flags/{flagKey}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetFlagRequest req, CancellationToken ct)
    {
        var flagKey = Route<string>("flagKey");
        if (flagKey is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        var cacheKey = $"flags:{req.EnvironmentId}:{flagKey}";

        var cachedValue = await _redis.StringGetAsync(cacheKey);
        if (cachedValue.HasValue)
        {
            await Send.OkAsync(new GetFlagResponse(flagKey, (bool)cachedValue), ct);
            return;
        }

        var flag = await _db.FeatureFlags
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EnvironmentId == req.EnvironmentId && x.Key == flagKey, ct);

        if (flag is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await _redis.StringSetAsync(cacheKey, flag.IsEnabled, TimeSpan.FromMinutes(10));
        await Send.OkAsync(new GetFlagResponse(flag.Key, flag.IsEnabled), ct);
    }
}