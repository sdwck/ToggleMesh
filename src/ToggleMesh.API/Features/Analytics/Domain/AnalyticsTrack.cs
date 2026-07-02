using System.Text.Json;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Analytics.Domain;

public class AnalyticsTrack : Entity
{
    public Guid EnvironmentId { get; set; }
    public string Identity { get; set; } = null!;
    public string EventName { get; set; } = null!;
    public double? Value { get; set; }
    public JsonDocument? Properties { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
