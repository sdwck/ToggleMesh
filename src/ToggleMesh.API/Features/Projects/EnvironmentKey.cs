using System.ComponentModel.DataAnnotations;

namespace ToggleMesh.API.Features.Projects;

public class EnvironmentKey
{
    public Guid Id { get; set; }
    [MaxLength(64)]
    public string ApiKey { get; set; } = string.Empty;
    public Guid EnvironmentId { get; set; }
    public ProjectEnvironment Environment { get; set; } = null!;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ExpireOn { get; set; } = null;
}