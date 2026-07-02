namespace ToggleMesh.API.Features.Webhooks.Domain;

public enum WebhookDeliveryStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Canceled = 3
}
