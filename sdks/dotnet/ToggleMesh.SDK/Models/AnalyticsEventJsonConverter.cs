using System.Text.Json;
using System.Text.Json.Serialization;

namespace ToggleMesh.SDK.Models;

public class AnalyticsEventJsonConverter : JsonConverter<AnalyticsEvent>
{
    public override AnalyticsEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserializing AnalyticsEvent is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, AnalyticsEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        writer.WriteNumber("Type", (int)value.Type);
        writer.WriteNumber("Timestamp", value.Timestamp);
        
        if (value.Identity != null)
            writer.WriteString("Identity", value.Identity);
        
        if (value.FlagKey != null)
            writer.WriteString("FlagKey", value.FlagKey);
            
        if (value.Type == AnalyticsEventType.Exposure)
        {
            writer.WriteBoolean("Result", value.Result);
        }
        else if (value.Type == AnalyticsEventType.Track)
        {
            if (value.EventName != null)
                writer.WriteString("EventName", value.EventName);
                
            if (value.Value.HasValue)
                writer.WriteNumber("Value", value.Value.Value);
        }

        writer.WritePropertyName("Properties");
        value.SerializeProperties(writer, options);

        writer.WriteEndObject();
    }
}
