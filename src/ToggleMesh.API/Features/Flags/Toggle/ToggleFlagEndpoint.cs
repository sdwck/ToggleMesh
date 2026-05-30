using FastEndpoints;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Toggle;

public class ToggleFlagEndpoint : Endpoint<ToggleFlagRequest>
{
    private readonly IHubContext<ToggleHub> _hubContext;
    private readonly AppDbContext _db;
    private readonly ILogger<ToggleFlagEndpoint> _logger;

    public ToggleFlagEndpoint(
        IHubContext<ToggleHub> hubContext, 
        AppDbContext db,
        ILogger<ToggleFlagEndpoint> logger)
    {
        _hubContext = hubContext;
        _db = db;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/flags/toggle");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ToggleFlagRequest req, CancellationToken ct)
    {
        var rowsAffected = await _db.FeatureFlags
            .Where(x => x.Key == req.Key)
            .ExecuteUpdateAsync(x =>
                x.SetProperty(xx => xx.IsEnabled, req.IsEnabled), ct);

        if (rowsAffected == 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            await _hubContext.Clients.All.SendAsync("FlagUpdated", req.Key, req.IsEnabled, ct);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to broadcast flag update");
        }

        await Send.OkAsync(new { req.Key, req.IsEnabled }, ct);
    }
}