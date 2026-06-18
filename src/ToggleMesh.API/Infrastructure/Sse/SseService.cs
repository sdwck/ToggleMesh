using System.Collections.Concurrent;
using System.Text.Json;

namespace ToggleMesh.API.Infrastructure.Sse;

public class SseService : ISseService
{
    private readonly ConcurrentDictionary<Guid, ClientConnection> _connections = new();

    private class ClientConnection
    {
        public Func<string, string, Task> OnMessage { get; }
        public CancellationToken CancellationToken { get; }

        public ClientConnection(Func<string, string, Task> onMessage, CancellationToken ct)
        {
            OnMessage = onMessage;
            CancellationToken = ct;
        }
    }

    public void Subscribe(Guid userId, Func<string, string, Task> onMessage, CancellationToken ct)
    {
        var connectionId = Guid.NewGuid();
        var connection = new ClientConnection(onMessage, ct);
        _connections.TryAdd(connectionId, connection);

        ct.Register(() =>
        {
            _connections.TryRemove(connectionId, out _);
        });
    }

    public async Task BroadcastAsync(string eventName, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var activeConnections = _connections.Values.ToList();
        
        var tasks = activeConnections.Select(async conn =>
        {
            if (conn.CancellationToken.IsCancellationRequested) return;
            try
            {
                await conn.OnMessage(eventName, json);
            }
            catch
            {
                // ignore
            }
        });

        await Task.WhenAll(tasks);
    }
}
