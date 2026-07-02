using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Analytics.Domain;

public class ContextualExperimentMetric : AuditableEntity
{
    public Guid EnvironmentId { get; set; }
    public string FlagKey { get; set; } = null!;
    public string EventName { get; set; } = null!;
    public bool Variant { get; set; }
    public Guid? RolloutId { get; set; }
    public string ContextSlice { get; set; } = null!;
    public long TotalExposures { get; set; }
    public long TotalConversions { get; set; }
    public double TotalValue { get; set; }
    public double SumOfSquaredValues { get; set; }
    public DateTimeOffset LastCalculatedAt { get; set; }
}
