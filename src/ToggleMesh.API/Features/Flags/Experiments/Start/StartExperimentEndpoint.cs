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
                .ThenInclude(x => x.Variations)
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
            ThrowError("Experiment is already active.");

        if (req.MabExplorationFloor is < 0 or > 10)
            ThrowError("Exploration floor must be between 0 and 10.");

        await _db.ExperimentMetrics
            .Where(x => x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ExecuteDeleteAsync(ct);

        await _db.AnalyticsExposures
            .Where(x => x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ExecuteDeleteAsync(ct);

        await _db.ContextualExperimentMetrics
            .Where(x => x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ExecuteDeleteAsync(ct);

        if (state.ContextualRollouts.Count > 0)
        {
            _db.ContextualRollouts.RemoveRange(state.ContextualRollouts);
            state.ContextualRollouts.Clear();
        }

        state.IsMabEnabled = req.Mode == "mab";
        state.MabGoalEvent = req.GoalEvent;
        state.MabOptimizationType = req.OptimizationType;
        state.MabExplorationFloor = req.MabExplorationFloor;
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

        if (req.BalanceWeights)
        {
            const int totalWeight = 10000;
            var variationsCount = state.FeatureFlag.Variations.Count;
            if (variationsCount > 0)
            {
                var baseWeight = totalWeight / variationsCount;
                var remainder = totalWeight % variationsCount;
                state.FallthroughRollout.Clear();
                var varList = state.FeatureFlag.Variations.ToList();
                for (var i = 0; i < varList.Count; i++)
                    state.FallthroughRollout.Add(new VariationWeight
                    {
                        VariationId = varList[i].Id,
                        Weight = baseWeight + (i < remainder ? 1 : 0)
                    });
            }
        }
        
        state.IsSrmAlertSent = false;
        state.SrmPValue = null;
        state.IsExperimentActive = true;
        state.ExperimentStartedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        var response = state.ToDto();
        await new NotifyFlagUpdatedCommand(
            environmentId, 
            flagKey, 
            response,
            state.ToSdkDto()
        ).ExecuteAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
