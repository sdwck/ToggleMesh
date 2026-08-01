using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Flags.Update;

public class UpdateFlagRequest
{
    public List<RuleInput>? Rules { get; set; } = null;
    public Guid? OffVariationId { get; set; }
    public List<VariationWeight>? FallthroughRollout { get; set; } = null;
    public Dictionary<string, Guid>? IndividualTargets { get; set; } = null;
    public bool? IsEnabled { get; set; }
}
