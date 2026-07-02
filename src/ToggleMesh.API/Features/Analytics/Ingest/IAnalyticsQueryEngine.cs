namespace ToggleMesh.API.Features.Analytics.Ingest;

public interface IAnalyticsQueryEngine
{
    Task AggregateMetricsAsync(CancellationToken ct = default);
    Task AggregateContextualMetricsAsync(CancellationToken ct = default);
    Task<IEnumerable<(DateTime Time, long Count)>> GetProjectHourlyEvaluationsAsync(Guid projectId, IEnumerable<Guid> environmentIds, TimeSpan duration, CancellationToken ct = default);
    Task<IEnumerable<ExperimentTimeSeriesPoint>> GetExperimentTimeSeriesAsync(Guid environmentId, string flagKey, string eventName, TimeSpan duration, CancellationToken ct = default);
}
