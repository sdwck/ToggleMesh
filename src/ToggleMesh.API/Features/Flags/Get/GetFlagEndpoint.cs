using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

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
        Policies($"Permission:{Auth.Models.Permissions.FlagsView}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("flagKey");
        if (flagKey is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        var cacheKey = $"flags:{environmentId}:{flagKey}";

        var cachedValue = await _redis.StringGetAsync(cacheKey);
        if (cachedValue.HasValue)
        {
            var cachedResponse = System.Text.Json.JsonSerializer.Deserialize<GetFlagResponse>((string)cachedValue!);
            if (cachedResponse is not null)
            {
                await Send.OkAsync(cachedResponse, ct);
                return;
            }
        }

        var flag = await _db.FeatureFlags
            .AsNoTracking()
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.EnvironmentId == environmentId && x.Key == flagKey, ct);

        if (flag is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = new GetFlagResponse(
            flag.Key, 
            flag.IsEnabled, 
            flag.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
            flag.RolloutPercentage);

        await _redis.StringSetAsync(cacheKey, System.Text.Json.JsonSerializer.Serialize(response), TimeSpan.FromMinutes(10));
        await Send.OkAsync(response, ct);
    }
}