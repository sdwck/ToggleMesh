namespace ToggleMesh.API.Features.Analytics.GetProjectExperiments;

public class ProjectExperimentSummaryDto
{
    public Guid EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = null!;
    public string FlagKey { get; set; } = null!;
    public string EventName { get; set; } = null!;
    public long TotalParticipants { get; set; }
    public DateTimeOffset LastCalculatedAt { get; set; }
    public double ProbabilityToBeatBaseline { get; set; }
    public double ExpectedUplift { get; set; }
    public double ExpectedValueUplift { get; set; }
    public bool IsRevenueBased { get; set; }
    public bool IsPrimaryGoal { get; set; }
    public bool IsExperimentActive { get; set; }
    public bool IsMabEnabled { get; set; }
    public bool HasRollout { get; set; }
}
