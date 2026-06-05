namespace ToggleMesh.API.Features.Projects.GetProject;

public class EnvironmentKeyDto
{
    public Guid Id { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}