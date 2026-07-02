namespace ToggleMesh.API.Features.Webhooks.Domain;

public enum WebhookStatus
{
    Active = 0,
    Failing = 1,
    DisabledBySystem = 2,
    Paused = 3
}
