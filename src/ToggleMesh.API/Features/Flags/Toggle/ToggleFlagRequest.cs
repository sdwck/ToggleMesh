namespace ToggleMesh.API.Features.Flags.Toggle;

public class ToggleFlagRequest
{
    public Guid EnvironmentId { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}