namespace ToggleMesh.API.Features.Integrations.Domain;

public record IntegrationDto(
    Guid Id,
    Guid ProjectId,
    IntegrationProvider Provider,
    string Name,
    string WebhookUrl,
    string[] Events,
    Guid[] EnvironmentIds,
    bool IsActive);
