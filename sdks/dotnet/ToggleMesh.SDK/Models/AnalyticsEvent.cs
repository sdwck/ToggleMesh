using System.Text.Json;
using System.Text.Json.Serialization;

namespace ToggleMesh.SDK.Models;

[JsonConverter(typeof(AnalyticsEventJsonConverter))]
public abstract class AnalyticsEvent
{
    public AnalyticsEventType Type { get; set; }
    public long Timestamp { get; set; }
    public string Identity { get; set; } = string.Empty;
    public string FlagKey { get; set; } = string.Empty;
    public Guid? Result { get; set; }
    public string EventName { get; set; } = string.Empty;
    public double? Value { get; set; }
    public Guid? VariationId { get; set; }
    public string? VariationValue { get; set; }

    public abstract void SerializeProperties(Utf8JsonWriter writer, JsonSerializerOptions options);
    public abstract void ReturnToPool();
}
