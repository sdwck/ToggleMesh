namespace ToggleMesh.API.Features.Metrics.Domain;

public record MetricPayloadDto(string Key, List<MetricVariationPayloadDto> Variations);
