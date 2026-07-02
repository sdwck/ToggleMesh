using FastEndpoints;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Streaming;

namespace ToggleMesh.API.Features.Streaming;

public class StreamEndpoint : EndpointWithoutRequest
{
    private readonly ISseConnectionManager _connectionManager;
    private readonly IApiKeyCacheService _apiKeyCache;

    public StreamEndpoint(
        ISseConnectionManager connectionManager,
        IApiKeyCacheService apiKeyCache)
    {
        _connectionManager = connectionManager;
        _apiKeyCache = apiKeyCache;
    }

    public override void Configure()
    {
        Get("/stream");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var apiKey = HttpContext.Request.Headers["x-api-key"].ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = HttpContext.Request.Query["apiKey"].ToString();
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            HttpContext.Response.StatusCode = 401;
            return;
        }

        var keyInfo = await _apiKeyCache.GetKeyInfoAsync(apiKey, ct);
        if (keyInfo is null)
        {
            HttpContext.Response.StatusCode = 401;
            return;
        }

        var envIdStr = keyInfo.EnvironmentId.ToString();
        var connectionId = Guid.NewGuid().ToString("N");

        HttpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        HttpContext.Response.Headers.Append("Cache-Control", "no-cache");
        HttpContext.Response.Headers.Append("Connection", "keep-alive");

        await HttpContext.Response.Body.FlushAsync(ct);

        _connectionManager.AddClient(envIdStr, connectionId, HttpContext.Response);

        try
        {
            await Task.Delay(Timeout.Infinite, HttpContext.RequestAborted);
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
        finally
        {
            _connectionManager.RemoveClient(envIdStr, connectionId);
        }
    }
}
