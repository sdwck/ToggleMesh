using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Sse;
using ToggleMesh.API.Persistence;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Memory;

namespace ToggleMesh.API.Features.Organizations.UpdateMember;

public class UpdateOrganizationMemberEndpoint : ToggleEndpoint<UpdateOrganizationMemberRequest>
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;
    private readonly ISseService _sseService;

    public UpdateOrganizationMemberEndpoint(AppDbContext db, IConnectionMultiplexer redis, IMemoryCache memoryCache, ISseService sseService)
    {
        _db = db;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
        _sseService = sseService;
    }

    public override void Configure()
    {
        Put("/organizations/{OrganizationId:guid}/members/{UserId:guid}");
        Version(1);
    }

    public override async Task HandleAsync(UpdateOrganizationMemberRequest req, CancellationToken ct)
    {
        var organizationId = Route<Guid>("OrganizationId");
        var userId = Route<Guid>("UserId");

        var currentUserMember = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == UserId, ct);

        if (currentUserMember is not { Role: OrganizationRole.Admin })
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        if (UserId == userId)
        {
            AddError("You cannot change your own organization role.");
            await Send.ErrorsAsync(400, cancellation: ct);
            return;
        }

        var memberToUpdate = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);

        if (memberToUpdate == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (memberToUpdate.Role == OrganizationRole.Admin && req.Role != OrganizationRole.Admin)
        {
            var adminCount = await _db.OrganizationMembers
                .CountAsync(m => m.OrganizationId == organizationId && m.Role == OrganizationRole.Admin, ct);
            
            if (adminCount <= 1)
            {
                AddError("Cannot demote the last administrator of the organization.");
                await Send.ErrorsAsync(400, cancellation: ct);
                return;
            }
        }

        memberToUpdate.Role = req.Role;
        await _db.SaveChangesAsync(ct);

        var projectIds = await _db.Projects
            .Where(p => p.OrganizationId == organizationId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var pId in projectIds)
        {
            var cacheKey = $"project-member-state:{pId}:{userId}";
            await _redis.KeyDeleteAsync(cacheKey);
            _memoryCache.Remove(cacheKey);
        }

        await _sseService.BroadcastAsync("invalidate", new { queryKey = new object[] { "organizations", organizationId, "members" } });
        await _sseService.BroadcastAsync("invalidate", new { queryKey = new object[] { "projects" } });

        await Send.NoContentAsync(ct);
    }
}
