using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Organizations;

namespace ToggleMesh.API.Features.Auth.Models;

public class ApplicationUser : IdentityUser<Guid>
{

    // ReSharper disable once CollectionNeverUpdated.Global
    public ICollection<ProjectMember> ProjectMembers { get; set; } = [];

    // ReSharper disable once CollectionNeverUpdated.Global
    public ICollection<OrganizationMember> OrganizationMembers { get; set; } = [];

    // ReSharper disable once CollectionNeverUpdated.Global
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}