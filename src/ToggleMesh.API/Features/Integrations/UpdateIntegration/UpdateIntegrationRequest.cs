namespace ToggleMesh.API.Features.Integrations.UpdateIntegration;

public class UpdateIntegrationRequest
{
    public Guid ProjectId { get; set; }
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string[] Events { get; set; } = [];
    public Guid[] EnvironmentIds { get; set; } = [];
    public bool IsActive { get; set; }
}
