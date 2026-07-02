using ToggleMesh.API.Features.Analytics.Domain;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public interface IAnalyticsStorageSink
{
    Task WriteBatchAsync(List<AnalyticsExposure> exposures, List<AnalyticsTrack> tracks, CancellationToken ct = default);
}
