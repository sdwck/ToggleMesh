using ToggleMesh.API.Features.Webhooks.Domain;

namespace ToggleMesh.API.Features.Webhooks.UpdateWebhookStatus;

public class UpdateWebhookStatusRequest
{
    public WebhookStatus Status { get; set; }
}