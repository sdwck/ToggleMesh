using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.Get;

public class GetFlagEndpoint : ToggleEndpointWithoutRequest<GetFlagResponse>
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
        Get("/projects/{projectId}/environments/{environmentId}/flags/{flagKey}");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsView);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("flagKey");

        if (flagKey is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var cacheKey = CacheKeys.FlagState(environmentId, flagKey);
        var cachedValue = await _redis.StringGetAsync(cacheKey);

        if (cachedValue.HasValue)
        {
            var cachedResponse = JsonSerializer.Deserialize<GetFlagResponse>((string)cachedValue!);
            if (cachedResponse is not null)
            {
                await Send.OkAsync(cachedResponse, ct);
                return;
            }
        }

        var state = await _db.FlagEnvironmentStates
            .AsNoTracking()
            .Include(x => x.FeatureFlag)
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.EnvironmentId == environmentId && x.FeatureFlag.Key == flagKey, ct);

        if (state is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = state.ToDto();

        var ttl = TimeSpan.FromMinutes(Config.GetValue("Caching:DefaultTtlMinutes", 10));
        await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(response), ttl);
        await Send.OkAsync(response, ct);
    }
}
