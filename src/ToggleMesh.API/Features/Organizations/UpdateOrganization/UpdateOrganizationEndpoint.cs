using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

namespace ToggleMesh.API.Features.Organizations.UpdateOrganization;

public class UpdateOrganizationEndpoint : ToggleEndpoint<UpdateOrganizationRequest>
{
    private readonly AppDbContext _db;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;

    public UpdateOrganizationEndpoint(AppDbContext db, IConnectionMultiplexer redis, IMemoryCache memoryCache)
    {
        _db = db;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
    }

    public override void Configure()
    {
        Put("/organizations/{OrganizationId:guid}");
        Version(1);
        PreProcessor<RequireOrgAdminPreProcessor<UpdateOrganizationRequest>>();
    }

    public override async Task HandleAsync(UpdateOrganizationRequest req, CancellationToken ct)
    {
        var organizationId = Route<Guid>("OrganizationId");
        
        if (string.IsNullOrWhiteSpace(req.Name))
            ThrowError("Organization name is required.", 400);

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (req.RequireTwoFactor && !org.RequireTwoFactor)
        {
            var hasMfaClaim = User.Claims.Any(c => c.Type == "amr" && c.Value == "mfa");
            if (!hasMfaClaim)
                ThrowError("You must enable Two-Factor Authentication on your own account before enforcing it for the organization.", 400);
        }

        var requireTwoFactorChanged = org.RequireTwoFactor != req.RequireTwoFactor;

        org.Name = req.Name.Trim();
        org.RequireTwoFactor = req.RequireTwoFactor;
        await _db.SaveChangesAsync(ct);

        if (requireTwoFactorChanged)
        {
            var projectIds = await _db.Projects
                .Where(p => p.OrganizationId == organizationId)
                .Select(p => p.Id)
                .ToListAsync(ct);

            var userIds = await _db.OrganizationMembers
                .Where(m => m.OrganizationId == organizationId)
                .Select(m => m.UserId)
                .ToListAsync(ct);

            var redisKeys = projectIds.SelectMany(pId => userIds.Select(uId => (RedisKey)CacheKeys.ProjectMemberState(pId, uId))).ToArray();

            if (redisKeys.Length > 0)
                await _redis.KeyDeleteAsync(redisKeys);

            foreach (var pId in projectIds)
                foreach (var uId in userIds)
                    _memoryCache.Remove(CacheKeys.ProjectMemberState(pId, uId));
        }

        await Send.NoContentAsync(ct);
    }
}
