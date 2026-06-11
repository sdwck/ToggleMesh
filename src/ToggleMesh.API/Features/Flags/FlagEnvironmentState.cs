using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Flags;

public class FlagEnvironmentState : IHasEnvironment
{
    public Guid Id { get; set; }
    
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
