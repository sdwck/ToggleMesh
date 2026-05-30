namespace ToggleMesh.API.Features.Flags.Toggle;

public class ToggleFlagRequest
{
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}