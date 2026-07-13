using ToggleMesh.API.Features.Integrations.Domain;

namespace ToggleMesh.API.Features.Integrations.CreateIntegration;

public class CreateIntegrationRequest
{
    public Guid ProjectId { get; set; }
    public IntegrationProvider Provider { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string[] Events { get; set; } = [];
    public Guid[] EnvironmentIds { get; set; } = [];
}
