using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Caching;
using StackExchange.Redis;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.UpdateProtection;

public class UpdateFlagProtectionEndpoint : ToggleEndpoint<UpdateFlagProtectionRequest>
{
    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ICacheInvalidator _cacheInvalidator;

    public UpdateFlagProtectionEndpoint(AppDbContext db, IConnectionMultiplexer redis, ICacheInvalidator cacheInvalidator)
    {
        _db = db;
        _redis = redis;
        _cacheInvalidator = cacheInvalidator;
    }

    public override void Configure()
    {
        Put("/projects/{projectId}/flags/{key}/protection");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.ProjectsEdit);
    }

    public override async Task HandleAsync(UpdateFlagProtectionRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var flagKey = Route<string>("key")!;

        var projectMember = await _db.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == UserId, ct);

        var orgMember = await _db.OrganizationMembers
            .Include(x => x.Organization)
            .ThenInclude(o => o.Projects)
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.Organization.Projects.Any(p => p.Id == projectId), ct);

        var isOwner = projectMember is { Role: ProjectRole.Owner } ||
                      orgMember is { Role: OrganizationRole.Admin };

        if (!isOwner)
            ThrowError("Only Project or Organization Owners can modify flag protection settings.", 403);

        var flag = await _db.FeatureFlags
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Key == flagKey, ct);

        if (flag is null)
            ThrowError("Flag not found", 404);

        flag.IsProtected = req.IsProtected;
        await _db.SaveChangesAsync(ct);

        try
        {
            var envIds = await _db.Environments
                .Where(e => e.ProjectId == projectId)
                .Select(e => e.Id)
                .ToListAsync(ct);

            var redis = _redis.GetDatabase();
            foreach (var envId in envIds)
            {
                var cacheKey = CacheKeys.FlagState(envId, flagKey);
                await redis.KeyDeleteAsync(cacheKey);
                await _cacheInvalidator.InvalidateEnvironmentCacheAsync(envId);
            }
        }
        catch
        {
            // ignored
        }

        await Send.OkAsync(new { flag.Key, flag.IsProtected }, ct);
    }
}
