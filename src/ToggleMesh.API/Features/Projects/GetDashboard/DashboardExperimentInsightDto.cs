namespace ToggleMesh.API.Features.Projects.GetDashboard;

public record DashboardExperimentInsightDto(
    string FlagKey, 
    string EventName,
    Guid EnvironmentId, 
    string EnvironmentName, 
    double ProbabilityToBeatBaseline, 
    double ExpectedUplift
);
