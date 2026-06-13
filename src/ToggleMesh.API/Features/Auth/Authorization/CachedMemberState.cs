using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Features.Auth.Authorization;

public record CachedMemberState(ProjectRole Role, Dictionary<Guid, ProjectRole> EnvironmentRoles);