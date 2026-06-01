namespace ToggleMesh.API.Features.Metrics;

public record MetricQueueItem(Guid EnvironmentId, string Key, long TrueCount, long FalseCount);