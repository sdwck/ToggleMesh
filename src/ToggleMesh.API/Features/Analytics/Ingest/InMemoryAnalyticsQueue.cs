using System.Threading.Channels;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class InMemoryAnalyticsQueue : IAnalyticsEventPublisher
{
    private readonly Channel<AnalyticsBatchMessage> _channel;

    public InMemoryAnalyticsQueue()
    {
        _channel = Channel.CreateBounded<AnalyticsBatchMessage>(new BoundedChannelOptions(10_000)
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
