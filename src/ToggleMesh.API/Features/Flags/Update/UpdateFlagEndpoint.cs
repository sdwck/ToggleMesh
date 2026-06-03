using FastEndpoints;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Update;

public class UpdateFlagEndpoint : Endpoint<UpdateFlagRequest, GetFlagResponse>
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ToggleHub> _hubContext;
    private readonly IDatabase _redis;
    private readonly ILogger<UpdateFlagEndpoint> _logger;

    public UpdateFlagEndpoint(
        AppDbContext db, 
        IHubContext<ToggleHub> hubContext, 
        IConnectionMultiplexer redis,
        ILogger<UpdateFlagEndpoint> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public override void Configure()
    {
        Put("/api/flags");
        AllowAnonymous();
    }

    public override async Task HandleAsync(UpdateFlagRequest req, CancellationToken ct)
    {
        var flag = await _db.FeatureFlags
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.EnvironmentId == req.EnvironmentId && x.Key == req.Key, ct);

        if (flag is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        flag.IsEnabled = req.IsEnabled;
        flag.RolloutPercentage = req.RolloutPercentage;

        var existingRules = flag.Rules.ToList();
        foreach (var oldRule in existingRules)
        {
            if (!req.Rules.Any(r => r.GroupId == oldRule.GroupId && r.Attribute == oldRule.Attribute && r.Operator == oldRule.Operator && r.Value == oldRule.Value))
                _db.Remove(oldRule);
        }
        
        foreach (var newRule in req.Rules)
        {
            if (!existingRules.Any(r => r.GroupId == newRule.GroupId && r.Attribute == newRule.Attribute && r.Operator == newRule.Operator && r.Value == newRule.Value))
            {
                flag.Rules.Add(new FlagRule { 
                    GroupId = newRule.GroupId, 
                    Attribute = newRule.Attribute, 
                    Operator = newRule.Operator, 
                    Value = newRule.Value 
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        var response = new GetFlagResponse(
            flag.Key, 
            flag.IsEnabled, 
            flag.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
            flag.RolloutPercentage);

        try
        {
            var cacheKey = $"flags:{req.EnvironmentId}:{req.Key}";
            await _redis.StringSetAsync(cacheKey, System.Text.Json.JsonSerializer.Serialize(response), TimeSpan.FromMinutes(10));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update Redis cache for flag {FlagKey}", req.Key);
        }

        try
        {
            await _hubContext.Clients
                .Group(req.EnvironmentId.ToString())
                .SendAsync("FlagUpdated", response, ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to broadcast flag update");
        }

        await Send.OkAsync(response, ct);
    }
}