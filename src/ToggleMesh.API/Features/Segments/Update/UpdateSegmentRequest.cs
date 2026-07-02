using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Segments.Update;

public class UpdateSegmentRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<RuleDto> Rules { get; set; } = new();
}
