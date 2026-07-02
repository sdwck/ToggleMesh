using FastEndpoints;

namespace ToggleMesh.API.Features.Projects.Domain;

public class GetProjectRoleCommand : ICommand<ProjectRoleResult>
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
}