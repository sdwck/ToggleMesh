namespace ToggleMesh.API.Features.Metrics.Ingest;

public record MetricPayloadDto(string Key, long TrueCount, long FalseCount);