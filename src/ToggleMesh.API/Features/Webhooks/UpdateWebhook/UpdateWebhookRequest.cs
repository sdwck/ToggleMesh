namespace ToggleMesh.API.Features.Webhooks.UpdateWebhook;

public class UpdateWebhookRequest
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public Guid[] EnvironmentIds { get; set; } = [];
    public string[] Events { get; set; } = [];
}
