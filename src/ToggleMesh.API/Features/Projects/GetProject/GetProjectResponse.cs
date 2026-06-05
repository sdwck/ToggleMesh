namespace ToggleMesh.API.Features.Projects.GetProject;

public class GetProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<EnvironmentDto> Environments { get; set; } = [];
}