namespace ToggleMesh.API.Features.Projects;

public record ProjectRoleResult(ProjectRole? Role, Dictionary<Guid, ProjectRole> EnvRoles);