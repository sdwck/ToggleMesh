namespace ToggleMesh.API.Infrastructure.Sse;

public interface ISseService
{
    Guid CreateConnection(Guid userId, Func<string, string, Task> onMessage, Action disconnect, CancellationToken ct);
    void SubscribeTopic(Guid connectionId, string topic);
    void UnsubscribeTopic(Guid connectionId, string topic);
    void RemoveConnection(Guid connectionId);
    bool VerifyConnectionOwner(Guid connectionId, Guid userId);
    void DisconnectUser(Guid userId);
    Task BroadcastAsync(string eventName, object data);
    Task BroadcastAsync(string topic, string eventName, object data);
}