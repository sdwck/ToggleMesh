namespace ToggleMesh.API.Features.Metrics.Domain;

public record MetricQueueItem(Guid EnvironmentId, string Key, bool IsClientSideExposed, long TrueCount, long FalseCount);