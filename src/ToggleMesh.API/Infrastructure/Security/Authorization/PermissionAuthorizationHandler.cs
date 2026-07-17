using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Infrastructure.Security.Authorization;

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

        if (requirement.Permission == Permissions.ProjectsCreate)
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
        else if (routeData.TryGetValue("envId", out var envIdValue2) && Guid.TryParse(envIdValue2?.ToString(), out var parsedEnvId2))
            environmentId = parsedEnvId2;

        if (!projectId.HasValue)
            return;

        var ct = httpContext.RequestAborted;
        if (environmentId.HasValue)
        {
            var envExists = await _dbContext.Environments
                .AsNoTracking()
                .AnyAsync(e => e.Id == environmentId.Value && e.ProjectId == projectId.Value, ct);
            if (!envExists)
                return;
        }

        var userIdString = context.User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                     ?? context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        if (userIdString == null || !Guid.TryParse(userIdString, out var userId))
            return;
        
        var cacheKey = CacheKeys.ProjectMemberState(projectId.Value, userId);

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
                .Include(om => om.Organization)
                .FirstOrDefaultAsync(om => om.OrganizationId == project.OrganizationId && om.UserId == userId, ct);

            if (orgMember == null) 
                return null;
                
            var require2fa = orgMember.Organization.RequireTwoFactor;

            if (orgMember.Role == OrganizationRole.Admin)
            {
                var state = new CachedMemberState(ProjectRole.Owner, new Dictionary<Guid, ProjectRole>(), require2fa);
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
            var resultState = new CachedMemberState(projectMember.Role, envRoles, require2fa);
            
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
        {
            if (memberState.OrgRequiresTwoFactor)
            {
                var hasMfaClaim = context.User.Claims.Any(c => c.Type == "amr" && c.Value == "mfa");
                if (!hasMfaClaim)
                {
                    httpContext.Response.Headers.Append("X-Requires-TwoFactor", "true");
                    context.Fail(new AuthorizationFailureReason(this, "TwoFactorRequired"));
                    return;
                }
            }
            context.Succeed(requirement);
        }
    }
}