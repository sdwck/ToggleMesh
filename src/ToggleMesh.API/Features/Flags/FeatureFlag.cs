using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Features.Flags;

public class FeatureFlag 
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
}