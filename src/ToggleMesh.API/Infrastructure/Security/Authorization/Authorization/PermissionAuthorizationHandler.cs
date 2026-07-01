using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Auth.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _dbContext;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;

    public PermissionAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor, 
        AppDbContext dbContext, 
        IConnectionMultiplexer redis,
        IMemoryCache memoryCache)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
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
                     ?? context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        if (userIdString == null || !Guid.TryParse(userIdString, out var userId))
            return;
        
        var cacheKey = CacheKeys.ProjectMemberState(projectId.Value, userId);
        var ct = httpContext.RequestAborted;

        var memberState = await _memoryCache.GetOrCreateAsync<CachedMemberState?>(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var redisValue = await _redis.StringGetAsync(cacheKey);

            if (redisValue.HasValue)
                return JsonSerializer.Deserialize<CachedMemberState>((string)redisValue!);

            var project = await _dbContext.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId.Value, ct);
            
            if (project == null) 
                return null;

            var orgMember = await _dbContext.OrganizationMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(om => om.OrganizationId == project.OrganizationId && om.UserId == userId, ct);

            if (orgMember == null) 
                return null;

            if (orgMember.Role == Organizations.OrganizationRole.Admin)
            {
                var state = new CachedMemberState(ProjectRole.Owner, new Dictionary<Guid, ProjectRole>());
                await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(state), TimeSpan.FromMinutes(5));
                return state;
            }
            

            var projectMember = await _dbContext.ProjectMembers
                .AsNoTracking()
                .Include(pm => pm.EnvironmentRoles)
                .FirstOrDefaultAsync(pm => 
                    pm.ProjectId == projectId.Value &&
                    pm.UserId == userId, ct);

            if (projectMember == null)
                return null;

            var envRoles = projectMember.EnvironmentRoles.ToDictionary(er => er.EnvironmentId, er => er.Role);
            var resultState = new CachedMemberState(projectMember.Role, envRoles);
            
            await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(resultState), TimeSpan.FromMinutes(5));
            return resultState;
        });
        
        if (memberState == null)
            return;

        var effectiveRole = memberState.Role;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (environmentId.HasValue 
            && memberState.EnvironmentRoles != null 
            && memberState.Role != ProjectRole.Owner 
            && memberState.Role != ProjectRole.Admin)
            if (memberState.EnvironmentRoles.TryGetValue(environmentId.Value, out var envRoleOverride))
                effectiveRole = envRoleOverride;

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