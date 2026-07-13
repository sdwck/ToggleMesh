namespace ToggleMesh.API.Features.Integrations.Domain;

public record IntegrationEvent(
    string EventName,
    string ProjectName,
    string? EnvironmentName,
    string? FlagKey,
    string? ActorEmail,
    DateTimeOffset Timestamp,
    string? AdminBaseUrl,
    string? ContextMessage = null);
