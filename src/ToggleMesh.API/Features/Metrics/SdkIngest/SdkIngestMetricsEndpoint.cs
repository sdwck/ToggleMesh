using System.Threading.Channels;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Metrics.SdkIngest;

public class SdkIngestMetricsEndpoint : ToggleEndpoint<SdkIngestMetricsRequest>
{
    private readonly Channel<MetricQueueItem> _channel;

    public SdkIngestMetricsEndpoint(Channel<MetricQueueItem> channel)
    {
        _channel = channel;
    }

    public override void Configure()
    {
        Post("/sdk/metrics");
        Version(1);
        AllowAnonymous();
        PreProcessor<ApiKeyPreProcessor<SdkIngestMetricsRequest>>();
        Options(x => x.RequireCors("PublicSdk"));
    }

    public override async Task HandleAsync(SdkIngestMetricsRequest req, CancellationToken ct)
    {
        if (req.Metrics.Count == 0)
        {
            await Send.OkAsync(cancellation: ct);
            return;
        }
        
        foreach (var metric in req.Metrics)
        {
            var item = new MetricQueueItem(
                req.EnvId, 
                metric.Key, 
                req.KeyType == KeyType.Client,
                metric.TrueCount, 
                metric.FalseCount);
            
            await _channel.Writer.WriteAsync(item, ct);
        }

        await Send.ResponseAsync(null, 202, ct);
    }
}