using System.Collections.Concurrent;
using System.Text.Json;
using StackExchange.Redis;

namespace ToggleMesh.API.Infrastructure.Sse;

public class SseService : ISseService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, ClientConnection> _connections = new();
    private readonly ISubscriber _subscriber;
    private readonly RedisChannel _channel = RedisChannel.Literal("sse-broadcast");

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

    public SseService(IConnectionMultiplexer redis)
    {
        _subscriber = redis.GetSubscriber();
        _subscriber.Subscribe(_channel, OnRedisMessage);
    }

    private void OnRedisMessage(RedisChannel channel, RedisValue message)
    {
        if (message.IsNullOrEmpty) 
            return;

        var payload = JsonSerializer.Deserialize<SseMessagePayload>((string)message!);
        if (payload == null) 
            return;

        var activeConnections = _connections.Values.ToList();
        foreach (var conn in activeConnections)
        {
            if (conn.CancellationToken.IsCancellationRequested) continue;
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await conn.OnMessage(payload.EventName, payload.DataJson);
                }
                catch
                {
                    // ignore
                }
            });
        }
    }

    public void Subscribe(Guid userId, Func<string, string, Task> onMessage, CancellationToken ct)
    {
        var connectionId = Guid.CreateVersion7();
        var connection = new ClientConnection(onMessage, ct);
        _connections.TryAdd(connectionId, connection);

        ct.Register(() =>
        {
            _connections.TryRemove(connectionId, out _);
        });
    }

    public async Task BroadcastAsync(string eventName, object data)
    {
        var dataJson = JsonSerializer.Serialize(data);
        var payload = new SseMessagePayload(eventName, dataJson);
        var json = JsonSerializer.Serialize(payload);

        await _subscriber.PublishAsync(_channel, json);
    }

    public void Dispose()
    {
        _subscriber.Unsubscribe(_channel);
    }

    private record SseMessagePayload(string EventName, string DataJson);
}
