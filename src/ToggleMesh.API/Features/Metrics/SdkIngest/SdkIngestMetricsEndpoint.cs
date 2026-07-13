using System.Threading.Channels;
using ToggleMesh.API.Features.Metrics.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Endpoints;

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
        Options(x => x.RequireRateLimiting("sdk"));
        
        var maxPayloadSize = Config.GetValue<long>("Ingestion:MaxPayloadSizeBytes", 5242880);
        Options(x => x.Add(b => b.Metadata.Add(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(maxPayloadSize))));
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
            if (metric.Variations == null || metric.Variations.Count == 0) 
                continue;
            
            foreach (var variation in metric.Variations)
            {
                if (variation.Count <= 0) 
                    continue;
                
                var item = new MetricQueueItem(
                    req.EnvId, 
                    metric.Key, 
                    req.KeyType == KeyType.Client,
                    variation.VariationId,
                    variation.Count);
                
                await _channel.Writer.WriteAsync(item, ct);
            }
        }

        await Send.ResponseAsync(null, 202, ct);
    }
}
