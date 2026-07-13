namespace ToggleMesh.API.Features.Analytics.GetExperimentTimeSeries;

public class GetExperimentTimeSeriesRequest
{
    public Guid ProjectId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string FlagKey { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public int Hours { get; set; } = 24;
}
