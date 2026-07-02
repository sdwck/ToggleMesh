namespace ToggleMesh.API.Infrastructure.Streaming;

public interface ISseConnectionManager
{
    void AddClient(string environmentId, string connectionId, HttpResponse response);
    void RemoveClient(string environmentId, string connectionId);
    Task BroadcastToEnvironmentAsync(string environmentId, string payload);
}
