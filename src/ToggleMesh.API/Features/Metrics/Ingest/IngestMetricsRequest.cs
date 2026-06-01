using FastEndpoints;

namespace ToggleMesh.API.Features.Metrics.Ingest;

public class IngestMetricsRequest
{
    [FromHeader("x-api-key")] 
    public string ApiKey { get; set; } = string.Empty;
    
    // ReSharper disable once CollectionNeverUpdated.Global
    [FromBody]
    public List<MetricPayloadDto> Metrics { get; set; } = [];
}