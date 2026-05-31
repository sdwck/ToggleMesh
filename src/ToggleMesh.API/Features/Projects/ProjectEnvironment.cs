using ToggleMesh.API.Features.Flags;

namespace ToggleMesh.API.Features.Projects;

public class ProjectEnvironment
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public ICollection<FeatureFlag> FeatureFlags { get; set; } = [];
    public ICollection<EnvironmentKey> Keys { get; set; } = [];
}