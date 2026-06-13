using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Toggle;

public class ToggleFlagEndpoint : ToggleEndpoint<ToggleFlagRequest>
{
    private readonly IHubContext<ToggleHub> _hubContext;
    private readonly AppDbContext _db;
    private readonly ILogger<ToggleFlagEndpoint> _logger;
    private readonly IDatabase _redis;
    private readonly ICacheInvalidator _cacheInvalidator;

    public ToggleFlagEndpoint(
        IHubContext<ToggleHub> hubContext, 
        AppDbContext db,
        ILogger<ToggleFlagEndpoint> logger,
        IConnectionMultiplexer redis, 
        ICacheInvalidator cacheInvalidator)
    {
        _hubContext = hubContext;
        _db = db;
        _logger = logger;
        _cacheInvalidator = cacheInvalidator;
        _redis = redis.GetDatabase();
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/environments/{environmentId}/flags/{key}/toggle");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.FlagsToggle);
    }

    public override async Task HandleAsync(ToggleFlagRequest req, CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("key");

        var state = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.EnvironmentId == environmentId && x.FeatureFlag.Key == flagKey, ct);

        if (state is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        state.IsEnabled = req.IsEnabled;
        await _db.SaveChangesAsync(ct);

        var response = new GetFlagResponse(
            state.FeatureFlag.Key, 
            state.IsEnabled, 
            state.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
            state.RolloutPercentage,
            state.TrueCount,
            state.FalseCount);

        try
        {
            var cacheKey = $"flags:{environmentId}:{flagKey}";
            await _redis.StringSetAsync(cacheKey, System.Text.Json.JsonSerializer.Serialize(response), TimeSpan.FromMinutes(10));
            await _cacheInvalidator.InvalidateEnvironmentCacheAsync(environmentId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update Redis cache for flag {FlagKey}", flagKey);
        }

        try
        {
            await _hubContext.Clients
                .Group(environmentId.ToString())
                .SendAsync("FlagUpdated", response, ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to broadcast flag update");
        }

        await Send.OkAsync(new { flagKey, req.IsEnabled }, ct);
    }
}