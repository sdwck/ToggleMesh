namespace ToggleMesh.API.Features.Analytics.Ingest;

public class KafkaMessagePayload
{
    public Guid EnvironmentId { get; set; }
    public List<RawAnalyticsEventDto> Events { get; set; } = new();
}