namespace ToggleMesh.API.Features.Projects.GetProjects;

public class ProjectEnvironmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ActiveFlagsCount { get; set; }
    public int TotalFlagsCount { get; set; }
}