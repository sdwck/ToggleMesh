using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.SdkGetAll;

public class SdkGetFlagsEndpoint : ToggleEndpoint<SdkGetFlagsRequest, List<GetFlagResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;

    public SdkGetFlagsEndpoint(AppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis.GetDatabase();
    }

    public override void Configure()
    {
        Get("/sdk/flags");
        Version(1);
        AllowAnonymous();
        PreProcessor<ApiKeyPreProcessor<SdkGetFlagsRequest>>();
        Options(x => x.RequireCors("PublicSdk"));
        Options(x => x.RequireRateLimiting("sdk"));
    }

    public override async Task HandleAsync(SdkGetFlagsRequest req, CancellationToken ct)
    {
        if (req.KeyType == KeyType.Client)
        {
            AddError("Client keys are not supported.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var cacheKey = $"sdk:flags:states:{req.EnvId}";

        var redisValue = await _redis.StringGetAsync(cacheKey);

        if (redisValue.HasValue)
        {
            var cachedStates = JsonSerializer.Deserialize<List<GetFlagResponse>>((string)redisValue!);
            if (cachedStates is not null)
            {
                await Send.OkAsync(cachedStates, ct);
                return;
            }
        }

        var states = await _db.FlagEnvironmentStates
            .AsNoTracking()
            .Include(x => x.FeatureFlag)
            .Include(x => x.Rules)
            .Where(x => x.EnvironmentId == req.EnvId)
            .Select(state => new GetFlagResponse(
                state.FeatureFlag.Key,
                state.IsEnabled,
                state.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
                state.FeatureFlag.Tags,
                state.RolloutPercentage,
                state.TrueCount,
                state.FalseCount))
            .ToListAsync(ct);

        var json = JsonSerializer.Serialize(states);
        await _redis.StringSetAsync(cacheKey, json, TimeSpan.FromMinutes(10));

        await Send.OkAsync(states, ct);
    }
}