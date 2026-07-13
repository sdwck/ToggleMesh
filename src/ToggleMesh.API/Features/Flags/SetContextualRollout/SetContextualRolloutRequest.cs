using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Flags.SetContextualRollout;

public class SetContextualRolloutRequest
{
    public string ContextSlice { get; set; } = string.Empty;
    public List<VariationWeight> Rollout { get; set; } = [];
}
