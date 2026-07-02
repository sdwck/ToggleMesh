using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Sse;

namespace ToggleMesh.API.Features.RealTime.RealTimeStream;

public class RealTimeStreamEndpoint : ToggleEndpointWithoutRequest
{
    private readonly ISseService _sseService;

    public RealTimeStreamEndpoint(ISseService sseService)
    {
        _sseService = sseService;
    }

    public override void Configure()
    {
        Get("/realtime/stream");
        Version(1);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = HttpContext.Response;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";

        await response.WriteAsync("retry: 5000\n\n", ct);
        await response.Body.FlushAsync(ct);

        var tcs = new TaskCompletionSource();

        var connectionId = _sseService.CreateConnection(UserId, async (eventName, data) =>
        {
            try
            {
                await response.WriteAsync($"event: {eventName}\n", ct);
                await response.WriteAsync($"data: {data}\n\n", ct);
                await response.Body.FlushAsync(ct);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, () => tcs.TrySetResult(), ct);

        await response.WriteAsync("event: connected\n", ct);
        await response.WriteAsync($"data: {{\"connectionId\": \"{connectionId}\"}}\n\n", ct);
        await response.Body.FlushAsync(ct);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
            {
                if (tcs.Task.IsFaulted)
                    throw tcs.Task.Exception!;

                await response.WriteAsync(": ping\n\n", ct);
                await response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        finally
        {
            tcs.TrySetResult();
            _sseService.RemoveConnection(connectionId);
        }
    }
}
