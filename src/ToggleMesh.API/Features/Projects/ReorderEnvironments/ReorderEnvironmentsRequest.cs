namespace ToggleMesh.API.Features.Projects.ReorderEnvironments;

public class ReorderEnvironmentsRequest
{
    public List<Guid> EnvironmentIds { get; set; } = [];
}