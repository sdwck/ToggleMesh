namespace ToggleMesh.API.Features.Metrics;

public record MetricPayloadDto(string Key, long TrueCount, long FalseCount);