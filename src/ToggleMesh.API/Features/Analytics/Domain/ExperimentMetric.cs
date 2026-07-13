using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Analytics.Domain;

public class ExperimentMetric : AuditableEntity
{
    public Guid EnvironmentId { get; set; }
    public string FlagKey { get; set; } = null!;
    public string EventName { get; set; } = null!;
    public Guid VariationId { get; set; }
    public long TotalExposures { get; set; }
    public long TotalConversions { get; set; }
    public double TotalValue { get; set; }
    public double SumOfSquaredValues { get; set; }
    public DateTimeOffset LastCalculatedAt { get; set; }
    public bool IsAlertSent { get; set; }
}
