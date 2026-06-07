using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Features.Flags;

public class FeatureFlag
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public string Key { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FlagEnvironmentState> States { get; set; } = new List<FlagEnvironmentState>();
}
