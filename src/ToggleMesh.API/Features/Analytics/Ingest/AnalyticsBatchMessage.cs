namespace ToggleMesh.API.Features.Analytics.Ingest;

public class AnalyticsBatchMessage
{
    public Guid EnvironmentId { get; set; }
    public List<RawAnalyticsEventDto> Events { get; set; } = [];
}