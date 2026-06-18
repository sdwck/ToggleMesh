using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Hubs;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Delete;

public class DeleteFlagEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ToggleHub> _hubContext;
    private readonly ILogger<DeleteFlagEndpoint> _logger;
    private readonly IDatabase _redis;
    private readonly ICacheInvalidator _cacheInvalidator;

    public DeleteFlagEndpoint(
        AppDbContext db,
        IHubContext<ToggleHub> hubContext,
        ILogger<DeleteFlagEndpoint> logger,
        IConnectionMultiplexer redis,
        ICacheInvalidator cacheInvalidator)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
        _cacheInvalidator = cacheInvalidator;
        _redis = redis.GetDatabase();
    }

    public override void Configure()
    {
        Delete("/projects/{projectId:guid}/flags/{key}");
        Version(1);
        this.RequirePermission(Auth.Models.Permissions.FlagsDelete);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var key = Route<string>("key");

        var flag = await _db.FeatureFlags
            .Include(f => f.States)
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Key == key, ct);

        if (flag == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var environmentIds = flag.States.Select(s => s.EnvironmentId).ToList();

        _db.FeatureFlags.Remove(flag);
        await _db.SaveChangesAsync(ct);

        foreach (var envId in environmentIds)
        {
            try
            {
                var cacheKey = $"flags:{envId}:{key}";
                await _redis.KeyDeleteAsync(cacheKey);
                await _cacheInvalidator.InvalidateEnvironmentCacheAsync(envId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to delete Redis cache key for flag {FlagKey} in environment {EnvId}", key, envId);
            }

            try
            {
                await _hubContext.Clients
                    .Group(envId.ToString())
                    .SendAsync("FlagUpdated", new { Key = key, IsDeleted = true }, ct);
                
                await _hubContext.Clients
                    .Group(envId.ToString())
                    .SendAsync("StateReloadRequired", ct);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to broadcast deletion for flag {FlagKey} in environment {EnvId}", key, envId);
            }
        }

        await Send.NoContentAsync(ct);
    }
}
