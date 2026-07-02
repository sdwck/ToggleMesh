namespace ToggleMesh.API.Features.Analytics.Ingest;

public interface IAnalyticsEventPublisher
{
    ValueTask PublishBatchAsync(Guid environmentId, List<RawAnalyticsEventDto> events, CancellationToken ct = default);
}
