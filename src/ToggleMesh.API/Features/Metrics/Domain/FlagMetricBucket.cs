using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Features.Metrics.Domain;

public class FlagMetricBucket
{
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
    
    public string FlagKey { get; set; } = string.Empty;
    
    public DateTimeOffset TimestampBucket { get; set; }
    
    public Guid VariationId { get; set; }
    public long Count { get; set; }
}
