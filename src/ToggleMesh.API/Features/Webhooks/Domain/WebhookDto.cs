namespace ToggleMesh.API.Features.Webhooks.Domain;

public record WebhookDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Url,
    WebhookStatus Status,
    Guid[] EnvironmentIds,
    string[] Events,
    string[] FlagTags,
    int ConsecutiveFailures,
    DateTime? LastTriggeredAt);
