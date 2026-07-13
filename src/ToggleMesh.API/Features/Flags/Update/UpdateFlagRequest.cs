using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Flags.Update;

public class UpdateFlagRequest
{
    public List<RuleInput> Rules { get; set; } = [];
    public Guid? OffVariationId { get; set; }
    public List<VariationWeight> FallthroughRollout { get; set; } = [];
    public Dictionary<string, Guid>? IndividualTargets { get; set; }
}
