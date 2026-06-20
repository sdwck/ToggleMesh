using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence.Abstractions;

namespace ToggleMesh.API.Features.Flags;

public class FlagEnvironmentState : AuditableEntity, IHasEnvironment
{
    public Guid FeatureFlagId { get; set; }
    public FeatureFlag FeatureFlag { get; set; } = null!;
    
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
    
    public bool IsEnabled { get; set; }
    public int? RolloutPercentage { get; set; }
    
    public long TrueCount { get; set; }
    public long FalseCount { get; set; }
    
    public ICollection<FlagRule> Rules { get; set; } = new List<FlagRule>();
}
