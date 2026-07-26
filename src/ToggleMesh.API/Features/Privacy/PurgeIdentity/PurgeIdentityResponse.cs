namespace ToggleMesh.API.Features.Privacy.PurgeIdentity;

public class PurgeIdentityResponse
{
    public int ExposuresPurged { get; set; }
    public int TracksPurged { get; set; }
    public int TotalPurged => ExposuresPurged + TracksPurged;
    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
}
