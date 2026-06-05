using Microsoft.AspNetCore.Identity;
using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Features.Auth.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    // ReSharper disable once CollectionNeverUpdated.Global
    public ICollection<ProjectMember> ProjectMembers { get; set; } = [];
}