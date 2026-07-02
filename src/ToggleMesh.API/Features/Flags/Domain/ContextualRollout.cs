using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Flags.Domain;

public class ContextualRollout : Entity
{
    public Guid FlagEnvironmentStateId { get; set; }
    public FlagEnvironmentState FlagEnvironmentState { get; set; } = null!;
    
    public string ContextSlice { get; set; } = string.Empty;
    public int RolloutPercentage { get; set; }
    public bool IsAutoManaged { get; set; } = true;
}
