namespace ToggleMesh.API.Features.Webhooks.Domain;

public record WebhookEvent(
    Guid ProjectId,
    Guid? EnvironmentId,
    string EventName,
    string FlagKey,
    string? ContextMessage = null);