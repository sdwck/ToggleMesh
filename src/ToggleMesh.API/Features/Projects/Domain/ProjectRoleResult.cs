namespace ToggleMesh.API.Features.Projects.Domain;

public record ProjectRoleResult(ProjectRole? Role, Dictionary<Guid, ProjectRole> EnvRoles);