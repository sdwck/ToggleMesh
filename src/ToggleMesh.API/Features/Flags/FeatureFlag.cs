namespace ToggleMesh.API.Features.Flags;

public class FeatureFlag 
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}