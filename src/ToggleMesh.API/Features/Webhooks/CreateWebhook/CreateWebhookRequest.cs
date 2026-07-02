namespace ToggleMesh.API.Features.Webhooks.CreateWebhook;

public class CreateWebhookRequest
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public Guid[] EnvironmentIds { get; set; } = [];
    public string[] Events { get; set; } = [];
    public string[] FlagTags { get; set; } = [];
}