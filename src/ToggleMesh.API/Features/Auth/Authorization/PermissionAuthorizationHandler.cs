using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Features.Projects;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Persistence;
using StackExchange.Redis;

namespace ToggleMesh.API.Features.Auth.Authorization;

public record CachedMemberState(ProjectRole Role, Dictionary<Guid, ProjectRole> EnvironmentRoles);

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
            return;

        if (requirement.Permission == Permissions.ProjectsCreate || requirement.Permission == Permissions.ProjectsView)
        {
            context.Succeed(requirement);
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        var routeData = httpContext.Request.RouteValues;
        Guid? projectId = null;
        Guid? environmentId = null;

        if (routeData.TryGetValue("projectId", out var projectIdValue) && Guid.TryParse(projectIdValue?.ToString(), out var parsedId))
            projectId = parsedId;

        if (routeData.TryGetValue("environmentId", out var envIdValue) && Guid.TryParse(envIdValue?.ToString(), out var parsedEnvId))
            environmentId = parsedEnvId;

        if (!projectId.HasValue)
            return;

        var userIdString = context.User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                     ?? context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (userIdString == null || !Guid.TryParse(userIdString, out var userId))
            return;

        CachedMemberState memberState;
        var cacheKey = $"project-member-state:{projectId.Value}:{userId}";
        var cachedStateJson = await _redis.StringGetAsync(cacheKey);

        if (cachedStateJson.HasValue)
        {
            memberState = JsonSerializer.Deserialize<CachedMemberState>(cachedStateJson.ToString())!;
        }
        else
        {
            var projectMember = await _dbContext.ProjectMembers
                .AsNoTracking()
                .Include(pm => pm.EnvironmentRoles)
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId.Value && pm.UserId == userId);

            if (projectMember == null)
                return;

            var envRoles = projectMember.EnvironmentRoles.ToDictionary(er => er.EnvironmentId, er => er.Role);
            memberState = new CachedMemberState(projectMember.Role, envRoles);
            await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(memberState), TimeSpan.FromMinutes(5));
        }

        var effectiveRole = memberState.Role;
        if (environmentId.HasValue && memberState.EnvironmentRoles != null && memberState.Role != ProjectRole.Owner && memberState.Role != ProjectRole.Admin)
        {
            if (memberState.EnvironmentRoles.TryGetValue(environmentId.Value, out var envRoleOverride))
            {
                effectiveRole = envRoleOverride;
            }
        }

        var hasPermission = false;
        switch (effectiveRole)
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
            case ProjectRole.None:
                hasPermission = false;
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