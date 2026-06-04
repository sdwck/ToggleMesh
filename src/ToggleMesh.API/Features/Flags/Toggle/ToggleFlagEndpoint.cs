using FastEndpoints;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Toggle;

public class ToggleFlagEndpoint : Endpoint<ToggleFlagRequest>
{
    private readonly IHubContext<ToggleHub> _hubContext;
    private readonly AppDbContext _db;
    private readonly ILogger<ToggleFlagEndpoint> _logger;
    private readonly IDatabase _redis;

    public ToggleFlagEndpoint(
        IHubContext<ToggleHub> hubContext, 
        AppDbContext db,
        ILogger<ToggleFlagEndpoint> logger,
        IConnectionMultiplexer redis)
    {
        _hubContext = hubContext;
        _db = db;
        _logger = logger;
        _redis = redis.GetDatabase();
    }

    public override void Configure()
    {
        Post("/flags/toggle");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(ToggleFlagRequest req, CancellationToken ct)
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

        await Send.OkAsync(new { req.Key, req.IsEnabled }, ct);
    }
}