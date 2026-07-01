namespace ToggleMesh.API.Features.Analytics.GetUniqueEvents;

public class GetUniqueEventsRequest
{
    public Guid ProjectId { get; set; }
    public Guid EnvironmentId { get; set; }
}