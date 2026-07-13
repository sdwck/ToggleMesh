using System.Text.Json;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Analytics.Domain;

public class AnalyticsExposure : Entity
{
    public Guid EnvironmentId { get; set; }
    public string FlagKey { get; set; } = null!;
    public string Identity { get; set; } = null!;
    public Guid VariationId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public JsonDocument? Properties { get; set; }
}
