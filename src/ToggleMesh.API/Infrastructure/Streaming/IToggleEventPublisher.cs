namespace ToggleMesh.API.Infrastructure.Streaming;

public interface IToggleEventPublisher
{
    Task PublishEventAsync<T>(string environmentId, string eventName, T payload);
}
