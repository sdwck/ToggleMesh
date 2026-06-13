using Microsoft.Extensions.Caching.Hybrid;

namespace ToggleMesh.API.BackgroundServices.Caching.Handlers;

public class ApiKeyInvalidationHandler : ICacheInvalidationHandler
{
    private readonly HybridCache _cache;

    public ApiKeyInvalidationHandler(HybridCache cache)
    {
        _cache = cache;
    }

    public bool CanHandle(string message) => 
        message.StartsWith("apikey:", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(string message, CancellationToken ct)
    {
        await _cache.RemoveAsync(message, ct);
    }
}