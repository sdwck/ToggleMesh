using FastEndpoints;
using ToggleMesh.API.Features.Metrics.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Metrics.SdkIngest;

public class SdkIngestMetricsRequest : ISdkRequest
{
    [HideFromDocs]
    public Guid EnvId { get; set; }
    [HideFromDocs]
    public KeyType KeyType { get; set; }

    [FromBody]
    // ReSharper disable once CollectionNeverUpdated.Global
    public List<MetricPayloadDto> Metrics { get; set; } = [];
}