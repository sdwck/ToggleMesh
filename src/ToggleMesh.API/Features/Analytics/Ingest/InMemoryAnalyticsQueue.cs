using System.Threading.Channels;
using Microsoft.Extensions.Configuration;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class InMemoryAnalyticsQueue : IAnalyticsEventPublisher
{
    private readonly Channel<AnalyticsBatchMessage> _channel;

    public InMemoryAnalyticsQueue(IConfiguration configuration)
    {
        var capacity = configuration.GetValue<int>("Ingestion:AnalyticsQueueCapacity", 1000);
        
        _channel = Channel.CreateBounded<AnalyticsBatchMessage>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async ValueTask PublishBatchAsync(Guid environmentId, List<RawAnalyticsEventDto> events, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(new AnalyticsBatchMessage
        {
            EnvironmentId = environmentId,
            Events = events
        }, ct);
    }

    public IAsyncEnumerable<AnalyticsBatchMessage> ReadAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
