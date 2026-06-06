using Microsoft.AspNetCore.Authorization;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Features.Projects;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Persistence;
using StackExchange.Redis;

namespace ToggleMesh.API.Features.Auth.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _dbContext;
    private readonly IDatabase _redis;

    public PermissionAuthorizationHandler(IHttpContextAccessor httpContextAccessor, AppDbContext dbContext, IConnectionMultiplexer redis)     
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _redis = redis.GetDatabase();
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var isOwner = context.User.HasClaim(c => c is { Type: "role", Value: "Owner" });
        if (isOwner)
        {
            if (Permissions.OwnerPermissions.Contains(requirement.Permission))
            {
                context.Succeed(requirement);
                return;
            }
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        var routeData = httpContext.Request.RouteValues;
        Guid? projectId;

        if (routeData.TryGetValue("projectId", out var projectIdValue) && Guid.TryParse(projectIdValue?.ToString(), out var parsedId))
            projectId = parsedId;
        else
            return;

        var userIdString = context.User.Claims.FirstOrDefault(c => c.Type == "id")?.Value
                     ?? context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (userIdString == null || !Guid.TryParse(userIdString, out var userId))
            return;

        ProjectRole role;
        var cacheKey = $"project-role:{projectId.Value}:{userId}";
        var cachedRole = await _redis.StringGetAsync(cacheKey);

        if (cachedRole.HasValue && Enum.TryParse<ProjectRole>(cachedRole.ToString(), out var parsedRole))
        {
            role = parsedRole;
        }
        else
        {
            var projectMember = await _dbContext.ProjectMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId.Value && pm.UserId == userId);

            if (projectMember == null)
                return;

            role = projectMember.Role;
            await _redis.StringSetAsync(cacheKey, role.ToString(), TimeSpan.FromMinutes(5));
        }

        var hasPermission = false;
        switch (role)
        {
            case ProjectRole.Owner:
                hasPermission = Permissions.OwnerPermissions.Contains(requirement.Permission);
                break;
            case ProjectRole.Admin:
                hasPermission = Permissions.AdminPermissions.Contains(requirement.Permission);
                break;
            case ProjectRole.Editor:
                hasPermission = Permissions.EditorPermissions.Contains(requirement.Permission);
                break;
            case ProjectRole.Viewer:
                hasPermission = Permissions.ViewerPermissions.Contains(requirement.Permission);
                break;
        }

        if (hasPermission)
            context.Succeed(requirement);
    }
}

public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options) : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName.Substring("Permission:".Length);
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return policy;
        }

        return await base.GetPolicyAsync(policyName);
    }
}