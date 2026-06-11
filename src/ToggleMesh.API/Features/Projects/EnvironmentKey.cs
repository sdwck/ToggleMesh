using System.ComponentModel.DataAnnotations;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Projects;

public class EnvironmentKey : IHasEnvironment
{
    public Guid Id { get; set; }
    public string KeyHash { get; set; } = string.Empty; 
    public string KeyPreview { get; set; } = string.Empty;
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ExpireOn { get; set; }
    public KeyType KeyType { get; set; }
}