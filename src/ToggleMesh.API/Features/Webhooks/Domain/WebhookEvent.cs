namespace ToggleMesh.API.Features.Webhooks;

public record WebhookEvent(
    Guid ProjectId,
    Guid? EnvironmentId,
    string EventName,
    string FlagKey);