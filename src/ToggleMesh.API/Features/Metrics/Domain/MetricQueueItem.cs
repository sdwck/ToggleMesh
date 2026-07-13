namespace ToggleMesh.API.Features.Metrics.Domain;

public record MetricQueueItem(Guid EnvironmentId, string Key, bool IsClientSideExposed, Guid VariationId, long Count);
