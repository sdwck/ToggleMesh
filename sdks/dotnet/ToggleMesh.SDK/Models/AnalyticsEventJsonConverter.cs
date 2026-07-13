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
        
        if (!string.IsNullOrEmpty(value.Identity))
            writer.WriteString("Identity", value.Identity);
        
        if (!string.IsNullOrEmpty(value.FlagKey))
            writer.WriteString("FlagKey", value.FlagKey);
            
        if (value.Type == AnalyticsEventType.Exposure)
        {
            if (value.VariationId.HasValue)
                writer.WriteString("VariationId", value.VariationId.Value.ToString());
            if (value.VariationValue != null)
                writer.WriteString("VariationValue", value.VariationValue);
        }
        else if (value.Type == AnalyticsEventType.Track)
        {
            if (!string.IsNullOrEmpty(value.EventName))
                writer.WriteString("EventName", value.EventName);
                
            if (value.Value.HasValue)
                writer.WriteNumber("Value", value.Value.Value);
        }

        writer.WritePropertyName("Properties");
        value.SerializeProperties(writer, options);

        writer.WriteEndObject();
    }
}
