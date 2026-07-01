using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Projects;

public class ProjectEnvironment : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<EnvironmentKey> Keys { get; set; } = [];
}