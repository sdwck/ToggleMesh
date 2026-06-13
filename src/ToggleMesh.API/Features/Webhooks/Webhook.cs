using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Features.Webhooks;

public class Webhook
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    public Guid[] EnvironmentIds { get; set; } = []; 
    public string[] Events { get; set; } = []; 
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }
}