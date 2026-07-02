using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Infrastructure.Sse;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;


namespace ToggleMesh.API.Features.Projects.RemoveMember;

public class RemoveMemberEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;
    private readonly ISseService _sseService;

    public RemoveMemberEndpoint(AppDbContext db, IConnectionMultiplexer redis, IMemoryCache memoryCache, ISseService sseService)
    {
        _db = db;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
        _sseService = sseService;
    }

    public override void Configure()
    {
        Delete("/projects/{projectId:guid}/members/{userId:guid}");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsManageMembers);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var userId = Route<Guid>("userId");
        
        if (UserId == userId)
            ThrowError("You cannot remove yourself from the project.", 400);

        var member = await _db.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId, ct);

        if (member == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var (currentUserRole, _) = await new GetProjectRoleCommand 
        { 
            ProjectId = projectId, 
            UserId = UserId 
        }.ExecuteAsync(ct);

        if (currentUserRole is null or > ProjectRole.Admin)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (!RoleHierarchy.CanManageMember(currentUserRole.Value, member.Role))
            ThrowError("You do not have permission to remove this member.", 403);

        _db.ProjectMembers.Remove(member);
        await _db.SaveChangesAsync(ct);

        var cacheKey = CacheKeys.ProjectMemberState(projectId, userId);
        await _redis.KeyDeleteAsync(cacheKey);
        _memoryCache.Remove(cacheKey);

        _sseService.DisconnectUser(userId);

        await Send.NoContentAsync(ct);
    }
}
