using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Sse;

namespace ToggleMesh.API.Features.Organizations.RemoveMember;

public class RemoveOrganizationMemberEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;
    private readonly ISseService _sseService;

    public RemoveOrganizationMemberEndpoint(AppDbContext db, IConnectionMultiplexer redis, IMemoryCache memoryCache, ISseService sseService)
    {
        _db = db;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
        _sseService = sseService;
    }

    public override void Configure()
    {
        Delete("/organizations/{OrganizationId:guid}/members/{UserId:guid}");
        Version(1);
        PreProcessor<RequireOrgAdminPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var organizationId = Route<Guid>("OrganizationId");
        var userId = Route<Guid>("UserId");
        
        if (UserId == userId)
            ThrowError("You cannot remove yourself from the organization.", 400);

        var memberToRemove = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);

        if (memberToRemove == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (memberToRemove.Role == OrganizationRole.Admin)
        {
            var adminCount = await _db.OrganizationMembers
                .CountAsync(m => m.OrganizationId == organizationId && m.Role == OrganizationRole.Admin, ct);
            
            if (adminCount <= 1)
                ThrowError("Cannot remove the last administrator of the organization.", 400);
        }

        _db.OrganizationMembers.Remove(memberToRemove);
        await _db.SaveChangesAsync(ct);

        var projectIds = await _db.Projects
            .Where(p => p.OrganizationId == organizationId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var redisKeys = projectIds
            .Select(pId => (RedisKey)CacheKeys.ProjectMemberState(pId, userId))
            .ToArray();

        if (redisKeys.Length > 0)
            await _redis.KeyDeleteAsync(redisKeys);

        foreach (var pId in projectIds)
            _memoryCache.Remove(CacheKeys.ProjectMemberState(pId, userId));

        await _sseService.BroadcastAsync("invalidate", new { queryKey = new object[] { "organizations", organizationId, "members" } });
        await _sseService.BroadcastAsync("invalidate", new { queryKey = new object[] { "projects" } });

        _sseService.DisconnectUser(userId);

        await Send.NoContentAsync(ct);
    }
}
