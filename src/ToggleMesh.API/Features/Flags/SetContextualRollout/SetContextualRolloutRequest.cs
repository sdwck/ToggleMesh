namespace ToggleMesh.API.Features.Flags.SetContextualRollout;

public class SetContextualRolloutRequest
{
    public string ContextSlice { get; set; } = null!;
    public int RolloutPercentage { get; set; }
}