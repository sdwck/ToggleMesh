using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Projects.Domain;

public class EnvironmentKey : Entity
{
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty; 
    public string KeyPreview { get; set; } = string.Empty;
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ExpireOn { get; set; }
    public KeyType KeyType { get; set; }
    public DateTime? LastUsedAt { get; set; }
}