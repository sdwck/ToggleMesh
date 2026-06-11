using FastEndpoints;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Metrics.Ingest;

public class IngestMetricsRequest : ISdkRequest
{
    [FromHeader("x-api-key")] 
    public string ApiKey { get; set; } = string.Empty;
    [HideFromDocs]
    public Guid EnvId { get; set; }
    [HideFromDocs]
    public KeyType KeyType { get; set; }

    [FromBody]
    public List<MetricPayloadDto> Metrics { get; set; } = [];
}