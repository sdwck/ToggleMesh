namespace ToggleMesh.API.BackgroundServices.Caching;

public interface ICacheInvalidationHandler
{
    bool CanHandle(string message);
    Task HandleAsync(string message, CancellationToken ct);
}