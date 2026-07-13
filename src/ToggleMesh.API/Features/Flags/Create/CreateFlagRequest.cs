using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Flags.Create;

public class CreateFlagRequest
{
    public string Key { get; set; } = string.Empty;
    public List<RuleInput> Rules { get; set; } = [];
    public Guid? OffVariationId { get; set; }
    public List<VariationWeight> FallthroughRollout { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public FlagType Type { get; set; } = FlagType.Boolean;
    public List<VariationDto>? Variations { get; set; }
}

