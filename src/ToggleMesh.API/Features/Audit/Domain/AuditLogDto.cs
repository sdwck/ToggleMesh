namespace ToggleMesh.API.Features.Audit;

public record AuditLogDto(
    Guid Id,
    Guid? EnvironmentId,
    string EntityName,
    string EntityFriendlyName,
    string EntityId,
    string Action,
    string OldValues,
    string NewValues,
    string PerformedBy,
    DateTime Timestamp);