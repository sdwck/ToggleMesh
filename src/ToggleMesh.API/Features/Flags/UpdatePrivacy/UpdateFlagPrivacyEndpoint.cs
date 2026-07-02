using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Infrastructure.Streaming;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.UpdatePrivacy;

public class UpdateFlagPrivacyEndpoint : ToggleEndpoint<UpdateFlagPrivacyRequest>
{
    private readonly AppDbContext _db;
    private readonly ICacheInvalidator _cacheInvalidator;
    private readonly IToggleEventPublisher _publisher;
    private readonly IDatabase _redis;
    private readonly ILogger<UpdateFlagPrivacyEndpoint> _logger;

    public UpdateFlagPrivacyEndpoint(
        AppDbContext db, 
        ICacheInvalidator cacheInvalidator, 
        IToggleEventPublisher publisher, 
        IConnectionMultiplexer redis,
        ILogger<UpdateFlagPrivacyEndpoint> logger)
    {
        _db = db;
        _cacheInvalidator = cacheInvalidator;
        _publisher = publisher;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public override void Configure()
    {
        Patch("/projects/{projectId:guid}/flags/{flagKey}/privacy");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(UpdateFlagPrivacyRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var flagKey = Route<string>("flagKey")!;

        var flag = await _db.FeatureFlags
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Key == flagKey, ct);

        if (flag == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        flag.IsClientSideExposed = req.IsClientSideExposed;
        await _db.SaveChangesAsync(ct);
        
        var environmentIds = await _db.Environments
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .Select(e => e.Id)
            .ToListAsync(ct);

        foreach (var envId in environmentIds)
        {
            try
            {
                var cacheKey = CacheKeys.FlagState(envId, flagKey);
                await _redis.KeyDeleteAsync(cacheKey);
                await _cacheInvalidator.InvalidateEnvironmentCacheAsync(envId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to invalidate cache for env {EnvId} during privacy update of flag {FlagKey}", envId, flagKey);
            }

            try
            {
                await _publisher.PublishEventAsync<object?>(
                    envId.ToString(),
                    "StateReloadRequired",
                    null
                );
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to broadcast StateReloadRequired for env {EnvId}", envId);
            }
        }

        await Send.NoContentAsync(ct);
    }
}
