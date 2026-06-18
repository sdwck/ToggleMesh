namespace ToggleMesh.API.Features.Projects.CreateProject;

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
}