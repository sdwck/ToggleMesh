using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Flags.Experiments.Start;

public class StartExperimentRequest
{
    public string Mode { get; set; } = "classic";
    public string GoalEvent { get; set; } = string.Empty;
    public MabOptimizationType OptimizationType { get; set; } = MabOptimizationType.Conversion;
    public string[] ContextPartitionKeys { get; set; } = [];
    public int? InitialRolloutPercentage { get; set; }
}
