namespace ToggleMesh.API.Infrastructure.Sse;

public interface ISseService
{
    void Subscribe(Guid userId, Func<string, string, Task> onMessage, CancellationToken ct);
    Task BroadcastAsync(string eventName, object data);
}