namespace ToggleMesh.API.Features.Flags.UpdateMetadata;

public class UpdateFlagMetadataRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
}