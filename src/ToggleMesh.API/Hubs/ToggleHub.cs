using Microsoft.AspNetCore.SignalR;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;

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
        
        var keyInfo = await _apiKeyCache.GetKeyInfoAsync(apiKey);

        if (keyInfo is null)
        {
            _logger.LogWarning("Connection aborted: Invalid or expired API key {ApiKeyPrefix}...", apiKey[..Math.Min(8, apiKey.Length)]);
            Context.Abort();
            return;
        }

        var envIdStr = keyInfo.EnvironmentId.ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, envIdStr);
        _logger.LogInformation("Client {ConnectionId} added to group {GroupName}", Context.ConnectionId, envIdStr);
        await base.OnConnectedAsync();
    }
}