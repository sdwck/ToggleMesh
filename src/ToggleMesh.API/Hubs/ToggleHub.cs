using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Hubs;

public class ToggleHub : Hub
{
    private readonly AppDbContext _db;

    private readonly ILogger<ToggleHub> _logger;

    public ToggleHub(AppDbContext db, ILogger<ToggleHub> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var apiKey = httpContext?.Request.Headers["x-api-key"].ToString() ?? 
                     httpContext?.Request.Query["apiKey"].ToString();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Connection aborted: API key is missing.");
            Context.Abort();
            return;
        }
        
        var envKey = await _db.EnvironmentKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApiKey == apiKey && (x.ExpireOn == null || x.ExpireOn > DateTime.UtcNow));

        if (envKey is null)
        {
            _logger.LogWarning("Connection aborted: Invalid or expired API key {ApiKey}", apiKey);
            Context.Abort();
            return;
        }
        
        var groupName = envKey.EnvironmentId.ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} added to group {GroupName}", Context.ConnectionId, groupName);
        await base.OnConnectedAsync();
    }
}