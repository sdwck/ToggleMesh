using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Segments.Create;

public class CreateSegmentRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<RuleInput> Rules { get; set; } = [];
}

