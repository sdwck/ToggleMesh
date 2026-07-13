using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Analytics.GetProjectExperiments;

public class GetProjectExperimentsEndpoint : ToggleEndpoint<GetProjectExperimentsRequest, List<ProjectExperimentSummaryDto>>
{
    private readonly AppDbContext _db;
    private readonly BayesianMathService _math;

    public GetProjectExperimentsEndpoint(AppDbContext db, BayesianMathService math)
    {
        _db = db;
        _math = math;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/experiments");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsView);
    }

    public override async Task HandleAsync(GetProjectExperimentsRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");

        var envs = await _db.Environments
            .Where(e => e.ProjectId == projectId)
            .ToDictionaryAsync(e => 
                e.Id, e => e.Name, ct);

        var envIds = envs.Keys.ToList();

        var allMetricsQuery = _db.ExperimentMetrics
            .AsNoTracking()
            .Where(m => envIds.Contains(m.EnvironmentId));

        if (!string.IsNullOrWhiteSpace(req.EnvironmentId) && 
            Guid.TryParse(req.EnvironmentId, out var fEnv))
            allMetricsQuery = allMetricsQuery
                .Where(m => m.EnvironmentId == fEnv);
        
        if (!string.IsNullOrWhiteSpace(req.FlagKey))
            allMetricsQuery = allMetricsQuery
                .Where(m => m.FlagKey.Contains(req.FlagKey));

        var allMetrics = await allMetricsQuery.ToListAsync(ct);

        var statesQuery = _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
            .AsNoTracking()
            .Where(x => envIds.Contains(x.EnvironmentId));
            
        if (!string.IsNullOrWhiteSpace(req.EnvironmentId) && 
            Guid.TryParse(req.EnvironmentId, out var fEnv2))
            statesQuery = statesQuery
                .Where(m => m.EnvironmentId == fEnv2);
        
        if (!string.IsNullOrWhiteSpace(req.FlagKey))
            statesQuery = statesQuery
                .Where(m => m.FeatureFlag.Key.Contains(req.FlagKey));

        var states = await statesQuery.ToListAsync(ct);

        var stateDict = 
            states.ToDictionary(x => (x.EnvironmentId, x.FeatureFlag.Key));

        var groupedMetrics = allMetrics
            .GroupBy(m => new { m.EnvironmentId, m.FlagKey, m.EventName })
            .ToList();

        var summaries = new List<ProjectExperimentSummaryDto>();

        foreach (var group in groupedMetrics)
        {
            var envId = group.Key.EnvironmentId;
            var flagKey = group.Key.FlagKey;
            var eventName = group.Key.EventName;

            stateDict.TryGetValue((envId, flagKey), out var state);

            if (state == null) 
                continue;

            if (req.IsActiveOnly && !state.IsExperimentActive)
                continue;

            var metrics = group.ToList();

            if (metrics.Count == 0)
            {
                summaries.Add(new ProjectExperimentSummaryDto
                {
                    EnvironmentId = envId,
                    EnvironmentName = envs[envId],
                    FlagKey = flagKey,
                    EventName = eventName,
                    TotalParticipants = 0,
                    LastCalculatedAt = DateTimeOffset.UtcNow,
                    ProbabilityToBeatBaseline = 0.5,
                    ExpectedUplift = 0,
                    IsPrimaryGoal = true,
                    IsExperimentActive = state.IsExperimentActive,
                    IsMabEnabled = state.IsMabEnabled,
                    HasRollout = state.FallthroughRollout.Count > 0
                });
                
                continue;
            }

            var totalExposures = metrics
                .Sum(x => x.TotalExposures);
            var maxLastCalculatedAt = metrics
                .Max(x => x.LastCalculatedAt);

            var prob = 0.5;
            double uplift = 0;
            double expectedValueUplift = 0;
            var isRevenueBased = false;
            
            var variations = metrics
                .Select(m => m.VariationId)
                .Distinct()
                .ToList();

            if (variations.Count >= 2)
            {
                var exposures = new long[variations.Count];
                var conversions = new long[variations.Count];
                var values = new double[variations.Count];
                var sumSquared = new double[variations.Count];

                for (var i = 0; i < variations.Count; i++)
                {
                    var m = metrics.First(x => x.VariationId == variations[i]);
                    exposures[i] = m.TotalExposures;
                    conversions[i] = m.TotalConversions;
                    values[i] = m.TotalValue;
                    sumSquared[i] = m.SumOfSquaredValues;
                }
                isRevenueBased = state.MabOptimizationType == MabOptimizationType.Revenue;

                double[] probs;
                if (!isRevenueBased)
                    probs = _math.CalculateDirichletProbabilities(exposures, conversions);
                else
                    probs = _math.CalculateDirichletProbabilities_Revenue(exposures, values, sumSquared);

                const int baselineIdx = 0;
                
                var maxUplift = variations.Count > 1 ? -double.MaxValue : 0;
                var maxValUplift = variations.Count > 1 ? -double.MaxValue : 0;
                
                for (var i = 0; i < variations.Count; i++)
                {
                    if (i == baselineIdx) 
                        continue;
                    
                    if (!isRevenueBased)
                    {
                        var up = _math.CalculateExpectedUplift(
                            exposures[baselineIdx], 
                            conversions[baselineIdx],
                            exposures[i], 
                            conversions[i]);
                        if (up > maxUplift) 
                            maxUplift = up;
                    }
                    else
                    {
                        var bArpu = 
                            exposures[baselineIdx] > 0 
                                ? values[baselineIdx] / exposures[baselineIdx] 
                                : 0;
                        var vArpu = 
                            exposures[i] > 0 
                                ? values[i] / exposures[i] 
                                : 0;
                        var up = 
                            bArpu > 0 
                                ? (vArpu - bArpu) / bArpu 
                                : 0;
                        if (up > maxValUplift) 
                            maxValUplift = up;
                    }
                }

                prob = probs.Max();
                uplift = maxUplift;
                expectedValueUplift = maxValUplift;
            }

            summaries.Add(new ProjectExperimentSummaryDto
            {
                EnvironmentId = envId,
                EnvironmentName = envs[envId],
                FlagKey = flagKey,
                EventName = eventName,
                TotalParticipants = totalExposures,
                LastCalculatedAt = maxLastCalculatedAt,
                ProbabilityToBeatBaseline = prob,
                ExpectedUplift = uplift,
                ExpectedValueUplift = expectedValueUplift,
                IsRevenueBased = isRevenueBased,
                IsPrimaryGoal = eventName == state.MabGoalEvent,
                IsExperimentActive = state.IsExperimentActive,
                IsMabEnabled = state.IsMabEnabled,
                HasRollout = state.FallthroughRollout.Count > 0
            });
        }

        var finalResponse = summaries
            .OrderByDescending(x => x.LastCalculatedAt)
            .ToList();

        await Send.OkAsync(finalResponse, ct);
    }
}
