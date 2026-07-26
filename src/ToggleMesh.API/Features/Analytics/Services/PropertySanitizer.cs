using System.Text.Json;

namespace ToggleMesh.API.Features.Analytics.Services;

public class PropertySanitizer : IPropertySanitizer
{
    private static readonly HashSet<string> BlockedPropertyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", 
        "e-mail", 
        "phone", 
        "telephone", 
        "mobile", 
        "ssn", 
        "social_security", 
        "password", 
        "credit_card", 
        "card_number", 
        "cvv"
    };

    public object? Sanitize(object? rawProps)
    {
        if (rawProps == null) 
            return null;

        try
        {
            var json = JsonSerializer.Serialize(rawProps);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return rawProps;

            var cleaned = new Dictionary<string, object?>();
            var modified = false;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (BlockedPropertyKeys.Contains(prop.Name))
                {
                    modified = true;
                    cleaned[prop.Name] = "[REDACTED_PII]";
                }
                else
                {
                    cleaned[prop.Name] = prop.Value.Clone();
                }
            }

            return modified ? cleaned : rawProps;
        }
        catch
        {
            return rawProps;
        }
    }
}
