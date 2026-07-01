using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Flags;

public class FlagEnvironmentState : AuditableEntity, IHasEnvironment
{
    public Guid FeatureFlagId { get; set; }
    public FeatureFlag FeatureFlag { get; set; } = null!;
    
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
    
    public bool IsEnabled { get; set; }
    public int? RolloutPercentage { get; set; }
    public bool IsMabEnabled { get; set; }
    public string? MabGoalEvent { get; set; }
    public MabOptimizationType MabOptimizationType { get; set; } = MabOptimizationType.Conversion;
    public string[] ContextPartitionKeys { get; set; } = [];
    
    public Dictionary<string, int>? ContextualRollouts { get; set; }
    
    public bool IsExperimentActive { get; set; }
    public DateTimeOffset? ExperimentStartedAt { get; set; }
    

    
    public ICollection<FlagRule> Rules { get; set; } = new List<FlagRule>();
}
