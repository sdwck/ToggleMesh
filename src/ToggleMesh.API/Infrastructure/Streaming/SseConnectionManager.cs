using System.Collections.Concurrent;

namespace ToggleMesh.API.Infrastructure.Streaming;

public class SseConnectionManager : ISseConnectionManager
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HttpResponse>> _clients = new();
    private readonly ILogger<SseConnectionManager> _logger;

    public SseConnectionManager(ILogger<SseConnectionManager> logger)
    {
        _logger = logger;
    }

    public void AddClient(string environmentId, string connectionId, HttpResponse response)
    {
        var envClients = _clients.GetOrAdd(
            environmentId, _ => new ConcurrentDictionary<string, HttpResponse>());
        envClients.TryAdd(connectionId, response);
        _logger.LogInformation("Added SSE client {ConnectionId} for environment {EnvironmentId}", connectionId, environmentId);
    }

    public void RemoveClient(string environmentId, string connectionId)
    {
        if (_clients.TryGetValue(environmentId, out var envClients))
        {
            if (envClients.TryRemove(connectionId, out _))
                _logger.LogInformation("Removed SSE client {ConnectionId} from environment {EnvironmentId}", connectionId, environmentId);

            if (envClients.IsEmpty)
                _clients.TryRemove(environmentId, out _);
        }
    }

    public async Task BroadcastToEnvironmentAsync(string environmentId, string payload)
    {
        if (!_clients.TryGetValue(environmentId, out var envClients))
            return;

        var message = $"data: {payload}\n\n";
        
        foreach (var client in envClients)
            try
            {
                await client.Value.WriteAsync(message);
                await client.Value.Body.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send message to SSE client {ConnectionId}", client.Key);
                RemoveClient(environmentId, client.Key);
            }
    }
}
