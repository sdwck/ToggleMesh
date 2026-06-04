using System.Threading.Channels;
using FastEndpoints;
using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Features.Metrics.Ingest;

public class IngestMetricsEndpoint : Endpoint<IngestMetricsRequest>
{
    private readonly IApiKeyCacheService _apiKeyCache;
    private readonly Channel<MetricQueueItem> _channel;

    public IngestMetricsEndpoint(IApiKeyCacheService apiKeyCache, Channel<MetricQueueItem> channel)
    {
        _apiKeyCache = apiKeyCache;
        _channel = channel;
    }

    public override void Configure()
    {
        Post("/sdk/metrics");
        Version(1);
        AllowAnonymous();
    }

    public override async Task HandleAsync(IngestMetricsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ApiKey) || req.Metrics.Count == 0)
        {
            await Send.OkAsync(cancellation: ct);
            return;
        }
        
        var envId = await _apiKeyCache.GetEnvironmentIdAsync(req.ApiKey, ct);

        if (envId == null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        
        foreach (var metric in req.Metrics)
        {
            var item = new MetricQueueItem(envId.Value, metric.Key, metric.TrueCount, metric.FalseCount);
            await _channel.Writer.WriteAsync(item, ct);
        }

        await Send.ResponseAsync(null, 202, ct);
    }
}