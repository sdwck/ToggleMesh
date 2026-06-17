namespace ToggleMesh.API.Features.Projects;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ProjectEnvironment> Environments { get; set; } = [];
    public ICollection<ProjectMember> Members { get; set; } = [];
}