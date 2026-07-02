namespace ToggleMesh.API.Features.Analytics.Ingest;

public class RawAnalyticsEventDto
{
    public AnalyticsEventType Type { get; set; }
    public long Timestamp { get; set; }
    public string Identity { get; set; } = null!;
    
    public string? FlagKey { get; set; }
    public bool Result { get; set; }
    
    public string? EventName { get; set; }
    public double? Value { get; set; }
    public object? Properties { get; set; }
}