using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Features.Projects.GetProject;
public class EnvironmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProjectRole UserRole { get; set; }
    public string? LastPiiBlockedContext { get; set; }
    public bool RequireApprovals { get; set; }
    public int RequiredApprovalsCount { get; set; }
    public bool RequireForProtectedFlagsOnly { get; set; }
    public List<EnvironmentKeyDto> Keys { get; set; } = [];
}