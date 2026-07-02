using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Features.Metrics.Domain;

public class FlagMetricBucket
{
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
    
    public string FlagKey { get; set; } = string.Empty;
    
    public DateTime TimestampBucket { get; set; }
    
    public long TrueCount { get; set; }
    public long FalseCount { get; set; }
}
