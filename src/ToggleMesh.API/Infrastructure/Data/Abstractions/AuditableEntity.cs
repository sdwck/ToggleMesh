namespace ToggleMesh.API.Infrastructure.Data.Abstractions;

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}