using System.Text.Json;

namespace ToggleMesh.SDK.Models;

public class PooledAnalyticsEvent<T> : AnalyticsEvent
{
    public T Properties { get; set; } = default!;

    public override void SerializeProperties(Utf8JsonWriter writer, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, Properties, options);
    }

    public override void ReturnToPool()
    {
        Identity = string.Empty;
        FlagKey = string.Empty;
        EventName = string.Empty;
        Value = null;
        Properties = default!;
        
        ObjectPools<T>.Pool.Return(this);
    }
}