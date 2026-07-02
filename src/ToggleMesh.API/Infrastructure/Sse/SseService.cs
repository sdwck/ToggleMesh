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
        public Guid UserId { get; }
        public Func<string, string, Task> OnMessage { get; }
        public CancellationToken CancellationToken { get; }
        public Action Disconnect { get; }
        public HashSet<string> Topics { get; } = new(StringComparer.OrdinalIgnoreCase) { "system" };

        public ClientConnection(Guid userId, Func<string, string, Task> onMessage, Action disconnect, CancellationToken ct)
        {
            UserId = userId;
            OnMessage = onMessage;
            Disconnect = disconnect;
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

        foreach (var kvp in _connections)
        {
            var conn = kvp.Value;
            if (conn.CancellationToken.IsCancellationRequested) continue;
            
            if (!conn.Topics.Contains(payload.Topic)) continue;

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

    public Guid CreateConnection(Guid userId, Func<string, string, Task> onMessage, Action disconnect, CancellationToken ct)
    {
        var connectionId = Guid.CreateVersion7();
        var connection = new ClientConnection(userId, onMessage, disconnect, ct);
        _connections.TryAdd(connectionId, connection);

        ct.Register(() =>
        {
            _connections.TryRemove(connectionId, out _);
        });
        
        return connectionId;
    }

    public void SubscribeTopic(Guid connectionId, string topic)
    {
        if (_connections.TryGetValue(connectionId, out var conn))
        {
            lock (conn.Topics)
            {
                conn.Topics.Add(topic);
            }
        }
    }

    public void UnsubscribeTopic(Guid connectionId, string topic)
    {
        if (_connections.TryGetValue(connectionId, out var conn))
        {
            lock (conn.Topics)
            {
                conn.Topics.Remove(topic);
            }
        }
    }

    public void RemoveConnection(Guid connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    public bool VerifyConnectionOwner(Guid connectionId, Guid userId)
    {
        if (_connections.TryGetValue(connectionId, out var conn))
        {
            return conn.UserId == userId;
        }
        return false;
    }

    public void DisconnectUser(Guid userId)
    {
        var userConnections = _connections.Where(c => c.Value.UserId == userId).Select(c => c.Key).ToList();
        foreach (var connId in userConnections)
        {
            if (_connections.TryRemove(connId, out var conn))
            {
                try
                {
                    conn.Disconnect?.Invoke();
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    public Task BroadcastAsync(string eventName, object data) => BroadcastAsync("system", eventName, data);

    public async Task BroadcastAsync(string topic, string eventName, object data)
    {
        var dataJson = JsonSerializer.Serialize(data);
        var payload = new SseMessagePayload(topic, eventName, dataJson);
        var json = JsonSerializer.Serialize(payload);

        await _subscriber.PublishAsync(_channel, json);
    }

    public void Dispose()
    {
        _subscriber.Unsubscribe(_channel);
    }

    private record SseMessagePayload(string Topic, string EventName, string DataJson);
}
