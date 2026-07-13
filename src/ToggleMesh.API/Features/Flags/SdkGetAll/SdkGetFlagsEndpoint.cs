using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Segments.Domain;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Flags.SdkGetAll;

public class SdkGetFlagsEndpoint : ToggleEndpoint<SdkGetFlagsRequest, SdkGetFlagsResponse>
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

        var cacheKey = CacheKeys.SdkFlagsStates(req.EnvId);

        var redisValue = await _redis.StringGetAsync(cacheKey);

        if (redisValue.HasValue && !string.IsNullOrWhiteSpace(redisValue))
        {
            try
            {
                var cachedResponse = JsonSerializer.Deserialize<SdkGetFlagsResponse>((string)redisValue!);
                if (cachedResponse is not null)
                {
                    await Send.OkAsync(cachedResponse, ct);
                    return;
                }
            }
            catch (JsonException)
            {
                // ignore
            }
        }

        var statesData = await _db.FlagEnvironmentStates
            .AsNoTracking()
            .Include(x => x.FeatureFlag)
                .ThenInclude(f => f.Variations)
            .Include(x => x.Rules)
            .Include(x => x.ContextualRollouts)
            .Where(x => x.EnvironmentId == req.EnvId)
            .AsSplitQuery()
            .ToListAsync(ct);

        var states = statesData.Select(state => state.ToSdkDto()).ToList();

        var segmentsData = await _db.Segments
            .AsNoTracking()
            .Include(x => x.Rules)
            .Where(x => x.EnvironmentId == req.EnvId)
            .AsSplitQuery()
            .ToListAsync(ct);

        var segments = segmentsData.Select(s => new SegmentDto(
                s.Id,
                s.EnvironmentId,
                s.Name,
                s.Description,
                s.Rules.Select(r => new RuleInput(r.GroupId, r.Attribute, r.Operator, r.Value)),
                s.CreatedAt)).ToList();

        var response = new SdkGetFlagsResponse(states, segments);

        var json = JsonSerializer.Serialize(response);
        var ttl = TimeSpan.FromMinutes(Config.GetValue("Caching:DefaultTtlMinutes", 10));
        await _redis.StringSetAsync(cacheKey, json, ttl);

        await Send.OkAsync(response, ct);
    }
}

