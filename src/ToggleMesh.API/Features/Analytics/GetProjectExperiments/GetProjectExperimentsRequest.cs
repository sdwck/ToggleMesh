namespace ToggleMesh.API.Features.Analytics.GetProjectExperiments;

public class GetProjectExperimentsRequest
{
    public string? EnvironmentId { get; set; }
    public string? FlagKey { get; set; }
    public bool IsActiveOnly { get; set; }
}