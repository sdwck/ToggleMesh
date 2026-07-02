using FastEndpoints;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class IngestEventsRequest : ISdkRequest
{
    [HideFromDocs]
    public Guid EnvId { get; set; }
    
    [HideFromDocs]
    public KeyType KeyType { get; set; }
    
    public List<RawAnalyticsEventDto> Events { get; set; } = [];
}