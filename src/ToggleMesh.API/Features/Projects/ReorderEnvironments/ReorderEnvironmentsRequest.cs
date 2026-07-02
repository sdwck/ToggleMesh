namespace ToggleMesh.API.Features.Projects.ReorderEnvironments;

public class ReorderEnvironmentsRequest
{
    // ReSharper disable once CollectionNeverUpdated.Global
    public List<Guid> EnvironmentIds { get; set; } = [];
}