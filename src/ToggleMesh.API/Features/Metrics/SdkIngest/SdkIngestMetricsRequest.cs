using FastEndpoints;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Metrics.SdkIngest;

public class SdkIngestMetricsRequest : ISdkRequest
{
    [HideFromDocs]
    public Guid EnvId { get; set; }
    [HideFromDocs]
    public KeyType KeyType { get; set; }

    [FromBody]
    public List<MetricPayloadDto> Metrics { get; set; } = [];
}