namespace ToggleMesh.API.Features.Projects.GetProjects;

public class ProjectListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EnvironmentCount { get; set; }
    public ProjectRole UserRole { get; set; }
    public List<ProjectEnvironmentDto> Environments { get; set; } = [];
}