namespace ToggleMesh.API.Features.Projects.GetDashboard;

public record ProjectDashboardDto(
    long ActiveFlagsCount,
    long EnvironmentsCount,
    long? FailingWebhooksCount,
    long MabActiveFlagsCount,
    IEnumerable<DashboardEvaluationPointDto> EvaluationsLast24Hours,
    IEnumerable<DashboardExperimentInsightDto> RecentExperiments
);
