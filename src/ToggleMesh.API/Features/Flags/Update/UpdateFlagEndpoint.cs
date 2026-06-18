using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Update;

public class UpdateFlagEndpoint : ToggleEndpoint<UpdateFlagRequest, GetFlagResponse>
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ToggleHub> _hubContext;
    private readonly IDatabase _redis;
    private readonly ILogger<UpdateFlagEndpoint> _logger;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateFlagEndpoint(
        AppDbContext db, 
        IHubContext<ToggleHub> hubContext, 
        IConnectionMultiplexer redis,
        ILogger<UpdateFlagEndpoint> logger, 
        ICacheInvalidator cacheInvalidator)
    {
        _db = db;
        _hubContext = hubContext;
        _redis = redis.GetDatabase();
        _logger = logger;
        _cacheInvalidator = cacheInvalidator;
    }

    public override void Configure()
    {
        Put("/projects/{projectId}/environments/{environmentId}/flags/{key}");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(UpdateFlagRequest req, CancellationToken ct)
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
        state.RolloutPercentage = req.RolloutPercentage;
        state.FeatureFlag.Tags = req.Tags.ToArray();

        var existingRules = state.Rules.ToList();
        foreach (var oldRule in existingRules)
            if (!req.Rules.Any(r => 
                    r.GroupId == oldRule.GroupId 
                    && r.Attribute == oldRule.Attribute 
                    && r.Operator == oldRule.Operator 
                    && r.Value == oldRule.Value))
                _db.Remove(oldRule);
        
        foreach (var newRule in req.Rules)
            if (!existingRules.Any(r => 
                    r.GroupId == newRule.GroupId 
                    && r.Attribute == newRule.Attribute 
                    && r.Operator == newRule.Operator 
                    && r.Value == newRule.Value))
                state.Rules.Add(new FlagRule { 
                    GroupId = newRule.GroupId, 
                    Attribute = newRule.Attribute, 
                    Operator = newRule.Operator, 
                    Value = newRule.Value 
                });

        state.FeatureFlag.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var response = new GetFlagResponse(
            state.FeatureFlag.Key, 
            state.IsEnabled, 
            state.Rules.Select(r => 
                new RuleDto(
                    r.GroupId, 
                    r.Attribute, 
                    r.Operator, 
                    r.Value)),
            state.FeatureFlag.Tags,
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

        await Send.OkAsync(response, ct);
    }
}