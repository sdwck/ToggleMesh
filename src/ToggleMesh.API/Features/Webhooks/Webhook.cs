using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Persistence.Abstractions;

namespace ToggleMesh.API.Features.Webhooks;

public class Webhook : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    public Guid[] EnvironmentIds { get; set; } = []; 
    public string[] Events { get; set; } = []; 
    
    public DateTime? LastTriggeredAt { get; set; }
}