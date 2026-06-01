using Microsoft.AspNetCore.SignalR;
using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Hubs;

public class ToggleHub : Hub
{
    private readonly IApiKeyCacheService _apiKeyCache;
    private readonly ILogger<ToggleHub> _logger;

    public ToggleHub(IApiKeyCacheService apiKeyCache, ILogger<ToggleHub> logger)
    {
        _apiKeyCache = apiKeyCache;
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
        
        var envId = await _apiKeyCache.GetEnvironmentIdAsync(apiKey);

        if (envId is null)
        {
            _logger.LogWarning("Connection aborted: Invalid or expired API key {ApiKey}", apiKey);
            Context.Abort();
            return;
        }
        
        var groupName = envId.Value.ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} added to group {GroupName}", Context.ConnectionId, groupName);
        await base.OnConnectedAsync();
    }
}