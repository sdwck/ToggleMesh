using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Integrations.Domain;

public class Integration : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public IntegrationProvider Provider { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;

    public string[] Events { get; set; } = [];
    public Guid[] EnvironmentIds { get; set; } = [];

    public bool IsActive { get; set; } = true;
}
