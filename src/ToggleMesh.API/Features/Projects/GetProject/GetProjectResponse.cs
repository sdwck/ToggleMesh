namespace ToggleMesh.API.Features.Projects.GetProject;

public class GetProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProjectRole UserRole { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<EnvironmentDto> Environments { get; set; } = [];
}