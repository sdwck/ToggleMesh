using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data.Abstractions;

namespace ToggleMesh.API.Infrastructure.Security.Authorization.Models;

public class ApplicationUser : IdentityUser<Guid>, ISoftDeletable
{

    // ReSharper disable once CollectionNeverUpdated.Global
    public ICollection<ProjectMember> ProjectMembers { get; set; } = [];

    // ReSharper disable once CollectionNeverUpdated.Global
    public ICollection<OrganizationMember> OrganizationMembers { get; set; } = [];

    // ReSharper disable once CollectionNeverUpdated.Global
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public bool IsDeleted { get; set; }
}