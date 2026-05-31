using FastEndpoints;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
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
        Post("/api/flags/toggle");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ToggleFlagRequest req, CancellationToken ct)
    {
        var rowsAffected = await _db.FeatureFlags
            .Where(x => x.EnvironmentId == req.EnvironmentId && x.Key == req.Key)
            .ExecuteUpdateAsync(x =>
                x.SetProperty(xx => xx.IsEnabled, req.IsEnabled), ct);

        if (rowsAffected == 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            var cacheKey = $"flags:{req.EnvironmentId}:{req.Key}";
            await _redis.StringSetAsync(cacheKey, req.IsEnabled, TimeSpan.FromMinutes(10));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update Redis cache for flag {FlagKey}", req.Key);
        }

        try
        {
            await _hubContext.Clients
                .Group(req.EnvironmentId.ToString())
                .SendAsync("FlagUpdated", req.Key, req.IsEnabled, ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to broadcast flag update");
        }

        await Send.OkAsync(new { req.Key, req.IsEnabled }, ct);
    }
}