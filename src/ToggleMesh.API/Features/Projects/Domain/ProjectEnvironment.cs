using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Features.Projects.Domain;

public class ProjectEnvironment : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
    public string? LastPiiBlockedContext { get; set; }
    public bool RequireApprovals { get; set; }
    public int RequiredApprovalsCount { get; set; } = 1;
    public bool RequireForProtectedFlagsOnly { get; set; }
    public ICollection<EnvironmentKey> Keys { get; set; } = [];
}