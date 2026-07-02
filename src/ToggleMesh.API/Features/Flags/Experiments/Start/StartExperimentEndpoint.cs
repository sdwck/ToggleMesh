using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.Experiments.Start;

public class StartExperimentEndpoint : ToggleEndpoint<StartExperimentRequest, GetFlagResponse>
{
    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    
    public StartExperimentEndpoint(AppDbContext db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/environments/{environmentId}/flags/{key}/experiments/start");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(StartExperimentRequest req, CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("key")!;

        var state = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
            .Include(x => x.Rules)
            .Include(x => x.ContextualRollouts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.EnvironmentId == environmentId && x.FeatureFlag.Key == flagKey, ct);

        if (state is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (state.IsExperimentActive)
        {
            ThrowError("Experiment is already active.");
            return;
        }

        await _db.ExperimentMetrics
            .Where(x => x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ExecuteDeleteAsync(ct);

        await _db.AnalyticsExposures
            .Where(x => x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ExecuteDeleteAsync(ct);

        await _db.ContextualExperimentMetrics
            .Where(x => x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ExecuteDeleteAsync(ct);

        state.ContextualRollouts.Clear();

        state.IsMabEnabled = req.Mode == "mab";
        state.MabGoalEvent = req.GoalEvent;
        state.MabOptimizationType = req.OptimizationType;
        if (req.ContextPartitionKeys is { Length: > 0 })
            state.ContextPartitionKeys = req.ContextPartitionKeys;
        else if (req.Mode == "mab")
        {
            var dbRedis = _redis.GetDatabase();
            var cacheKey = CacheKeys.FlagSchemaContextKeys(environmentId, flagKey);
            var keys = await dbRedis.SetMembersAsync(cacheKey);
            state.ContextPartitionKeys = keys.Length > 0 
                ? keys.Select(k => (string)k!).ToArray() : [];
        }
        else
            state.ContextPartitionKeys = [];

        if (req.InitialRolloutPercentage.HasValue)
            state.RolloutPercentage = req.InitialRolloutPercentage.Value;

        state.IsExperimentActive = true;
        state.ExperimentStartedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        var response = state.ToDto();
        await new NotifyFlagUpdatedCommand(
            environmentId, 
            flagKey, 
            response
        ).ExecuteAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
