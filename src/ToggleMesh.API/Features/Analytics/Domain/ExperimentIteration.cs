using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Analytics.Domain;

public class ExperimentIteration : AuditableEntity
{
    public Guid EnvironmentId { get; set; }
    public string FlagKey { get; set; } = null!;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public string FinalMetricsSnapshot { get; set; } = "[]";
    public string FlagConfigSnapshot { get; set; } = "{}";
}
