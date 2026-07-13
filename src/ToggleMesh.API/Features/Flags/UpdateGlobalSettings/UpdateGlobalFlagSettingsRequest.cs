using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Flags.UpdateGlobalSettings;

public class UpdateGlobalFlagSettingsRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string[] Tags { get; set; } = [];
    public List<VariationDto> Variations { get; set; } = [];
}
