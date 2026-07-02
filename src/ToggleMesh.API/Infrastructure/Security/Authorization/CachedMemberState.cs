using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Infrastructure.Security.Authorization;

public record CachedMemberState(ProjectRole Role, Dictionary<Guid, ProjectRole> EnvironmentRoles);