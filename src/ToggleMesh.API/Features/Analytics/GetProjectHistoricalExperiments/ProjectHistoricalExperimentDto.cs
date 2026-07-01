namespace ToggleMesh.API.Features.Analytics.GetProjectHistoricalExperiments;

public class ProjectHistoricalExperimentDto
{
    public Guid Id { get; set; }
    public Guid EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = null!;
    public string FlagKey { get; set; } = null!;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public string FinalMetricsSnapshot { get; set; } = "[]";
}