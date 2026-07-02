using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Webhooks.Domain;

public class Webhook : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public WebhookStatus Status { get; set; } = WebhookStatus.Active;
    public int ConsecutiveFailures { get; set; } = 0;
    
    public Guid[] EnvironmentIds { get; set; } = []; 
    public string[] Events { get; set; } = []; 
    public string[] FlagTags { get; set; } = [];
    
    public DateTime? LastTriggeredAt { get; set; }
}