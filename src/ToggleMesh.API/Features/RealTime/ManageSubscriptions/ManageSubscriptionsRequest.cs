namespace ToggleMesh.API.Features.RealTime.ManageSubscriptions;

public class ManageSubscriptionsRequest
{
    public Guid ConnectionId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
