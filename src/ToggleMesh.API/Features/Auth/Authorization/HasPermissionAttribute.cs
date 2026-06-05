using Microsoft.AspNetCore.Authorization;

namespace ToggleMesh.API.Features.Auth.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public string Permission { get; }

    public HasPermissionAttribute(string permission) : base(policy: $"Permission:{permission}")
    {
        Permission = permission;
    }
}