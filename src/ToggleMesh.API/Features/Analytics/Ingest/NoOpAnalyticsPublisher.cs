namespace ToggleMesh.API.Features.Analytics.Ingest;

public class NoOpAnalyticsPublisher : IAnalyticsEventPublisher
{
    public ValueTask PublishBatchAsync(Guid environmentId, List<RawAnalyticsEventDto> events, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }
}
