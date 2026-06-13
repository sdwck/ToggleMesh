namespace ToggleMesh.API.Persistence.Interceptors.Audit;

public record AuditMetadata(string FriendlyName, Guid? ProjectId, Guid? EnvironmentId);