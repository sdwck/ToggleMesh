namespace ToggleMesh.API.Features.Integrations.DeleteIntegration;

public class DeleteIntegrationRequest
{
    public Guid ProjectId { get; set; }
    public Guid Id { get; set; }
}