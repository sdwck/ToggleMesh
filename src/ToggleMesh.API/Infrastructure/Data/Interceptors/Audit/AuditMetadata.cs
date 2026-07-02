namespace ToggleMesh.API.Infrastructure.Data.Interceptors.Audit;

public record AuditMetadata(string FriendlyName, Guid? ProjectId, Guid? EnvironmentId);