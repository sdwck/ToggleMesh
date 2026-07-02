namespace ToggleMesh.API.Features.Analytics.GetSchema;

public record GetAnalyticsSchemaRequest
{
    public Guid ProjectId { get; init; }
    public Guid EnvironmentId { get; init; }
    public string? FlagKey { get; init; }
    public string? EventName { get; init; }
}
