namespace ToggleMesh.API.Features.Metrics.Domain;

public record MetricPayloadDto(string Key, long TrueCount, long FalseCount);