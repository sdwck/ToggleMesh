namespace ToggleMesh.API.Features.Projects.GetDashboard;

public record ProjectDashboardDto(
    long ActiveFlagsCount,
    long EnvironmentsCount,
    long? FailingWebhooksCount,
    long MabActiveFlagsCount,
    IEnumerable<DashboardEvaluationPointDto> EvaluationsLast24Hours,
    IEnumerable<DashboardExperimentInsightDto> RecentExperiments
);

public record DashboardEvaluationPointDto(DateTime Time, long Count);

public record DashboardExperimentInsightDto(
    string FlagKey, 
    string EventName,
    Guid EnvironmentId, 
    string EnvironmentName, 
    double ProbabilityToBeatBaseline, 
    double ExpectedUplift
);
