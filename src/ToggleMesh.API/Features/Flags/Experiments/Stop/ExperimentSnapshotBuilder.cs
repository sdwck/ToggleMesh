using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Analytics.GetContextualExperimentDetails;
using ToggleMesh.API.Features.Analytics.GetExperimentDetails;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Flags.Experiments.Stop;

public class ExperimentSnapshotBuilder
{
    private readonly AppDbContext _db;
    private readonly BayesianMathService _math;
    private readonly IAnalyticsQueryEngine _queryEngine;
    private readonly ILogger<ExperimentSnapshotBuilder> _logger;

    public ExperimentSnapshotBuilder(
        AppDbContext db,
        BayesianMathService math,
        IAnalyticsQueryEngine queryEngine,
        ILogger<ExperimentSnapshotBuilder> logger)
    {
        _db = db;
        _math = math;
        _queryEngine = queryEngine;
        _logger = logger;
    }

    public async Task<ExperimentSnapshots> BuildSnapshotAsync(
        Guid environmentId, 
        string flagKey, 
        FlagEnvironmentState state, 
        CancellationToken ct)
    {
        var metrics = await _db.ExperimentMetrics
            .AsNoTracking()
            .Where(x => 
                x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ToListAsync(ct);

        var events = metrics
            .Select(x => x.EventName)
            .Distinct()
            .ToList();
        var results = new List<ExperimentResultDto>();

        foreach (var evt in events)
        {
            var eventMetrics = metrics
                .Where(x => x.EventName == evt)
                .ToList();
            if (eventMetrics.Count == 0) 
                continue;

            var result = new ExperimentResultDto
            {
                EventName = evt,
                IsRevenueBased = state.MabOptimizationType == MabOptimizationType.Revenue,
                LastCalculatedAt = eventMetrics.Max(m => m.LastCalculatedAt)
            };

            var variations = eventMetrics
                .Select(m => m.VariationId)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (variations.Count >= 2)
            {
                var exposures = new long[variations.Count];
                var conversions = new long[variations.Count];
                var values = new double[variations.Count];
                var sumSquared = new double[variations.Count];

                for (var i = 0; i < variations.Count; i++)
                {
                    var m = eventMetrics
                        .First(x => x.VariationId == variations[i]);
                    exposures[i] = m.TotalExposures;
                    conversions[i] = m.TotalConversions;
                    values[i] = m.TotalValue;
                    sumSquared[i] = m.SumOfSquaredValues;
                }

                var probs = !result.IsRevenueBased 
                    ? _math.CalculateDirichletProbabilities(exposures, conversions) 
                    : _math.CalculateDirichletProbabilities_Revenue(
                        exposures, values, sumSquared);

                const int baselineIdx = 0;

                for (var i = 0; i < variations.Count; i++)
                {
                    double uplift = 0;
                    if (i != baselineIdx)
                    {
                        if (!result.IsRevenueBased)
                            uplift = _math.CalculateExpectedUplift(
                                exposures[baselineIdx], conversions[baselineIdx],
                                exposures[i], conversions[i]);
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
                            uplift = 
                                bArpu > 0 
                                    ? (vArpu - bArpu) / bArpu 
                                    : 0;
                        }
                    }

                    result.Variations.Add(new ExperimentVariationResultDto
                    {
                        VariationId = variations[i],
                        Exposures = exposures[i],
                        Conversions = conversions[i],
                        TotalValue = values[i],
                        ProbabilityToBeatBaseline = probs[i],
                        ExpectedUplift = uplift,
                        RolloutWeight = state.FallthroughRollout?
                            .FirstOrDefault(x => 
                                x.VariationId == variations[i])?.Weight ?? 0
                    });
                }
            }
            else if (variations.Count == 1)
            {
                var m = eventMetrics.First();
                result.Variations.Add(new ExperimentVariationResultDto
                {
                    VariationId = m.VariationId,
                    Exposures = m.TotalExposures,
                    Conversions = m.TotalConversions,
                    TotalValue = m.TotalValue,
                    ProbabilityToBeatBaseline = 1.0,
                    ExpectedUplift = 0
                });
            }

            results.Add(result);
        }

        var contextualMetrics = await _db.ContextualExperimentMetrics
            .AsNoTracking()
            .Where(x => x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ToListAsync(ct);

        var slices = contextualMetrics
            .Select(x => x.ContextSlice)
            .Distinct();
        var contextualResults = new List<ContextualExperimentResultDto>();

        foreach (var slice in slices)
        {
            var sliceEvents = contextualMetrics
                    .Where(x => x.ContextSlice == slice)
                    .Select(x => x.EventName).Distinct();

            foreach (var evt in sliceEvents)
            {
                var eventMetrics = contextualMetrics
                    .Where(x => 
                        x.ContextSlice == slice && x.EventName == evt)
                    .ToList();
                if (eventMetrics.Count == 0) 
                    continue;

                var isAutoManaged = true;
                var r = state.ContextualRollouts?
                    .FirstOrDefault(x => x.ContextSlice == slice);
                if (r != null)
                    isAutoManaged = r.IsAutoManaged;

                var result = new ContextualExperimentResultDto
                {
                    ContextSlice = slice,
                    EventName = evt,
                    IsRevenueBased = state.MabOptimizationType == MabOptimizationType.Revenue,
                    LastCalculatedAt = eventMetrics
                        .Max(m => m.LastCalculatedAt),
                    IsAutoManaged = isAutoManaged
                };

                var variations = eventMetrics
                    .Select(m => m.VariationId)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                if (variations.Count >= 2)
                {
                    var exposures = new long[variations.Count];
                    var conversions = new long[variations.Count];
                    var values = new double[variations.Count];
                    var sumSquared = new double[variations.Count];

                    for (var i = 0; i < variations.Count; i++)
                    {
                        var m = eventMetrics
                            .First(x => x.VariationId == variations[i]);
                        exposures[i] = m.TotalExposures;
                        conversions[i] = m.TotalConversions;
                        values[i] = m.TotalValue;
                        sumSquared[i] = m.SumOfSquaredValues;
                    }

                    var probs = !result.IsRevenueBased 
                        ? _math.CalculateDirichletProbabilities(exposures, conversions) 
                        : _math.CalculateDirichletProbabilities_Revenue(
                            exposures, values, sumSquared);

                    const int baselineIdx = 0;

                    for (var i = 0; i < variations.Count; i++)
                    {
                        double uplift = 0;
                        if (i != baselineIdx)
                        {
                            if (!result.IsRevenueBased)
                                uplift = _math.CalculateExpectedUplift(
                                    exposures[baselineIdx], 
                                    conversions[baselineIdx],
                                    exposures[i], 
                                    conversions[i]);
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
                                uplift = 
                                    bArpu > 0 
                                        ? (vArpu - bArpu) / bArpu 
                                        : 0;
                            }
                        }

                        result.Variations.Add(new ContextualExperimentVariationResultDto
                        {
                            VariationId = variations[i],
                            Exposures = exposures[i],
                            Conversions = conversions[i],
                            TotalValue = values[i],
                            ProbabilityToBeatBaseline = probs[i],
                            ExpectedUplift = uplift,
                            RolloutWeight = state.ContextualRollouts?
                                .FirstOrDefault(x => 
                                    x.ContextSlice == slice)?
                                .Rollout
                                .FirstOrDefault(x => 
                                    x.VariationId == variations[i])?
                                .Weight ?? 0
                        });
                    }
                }
                else if (variations.Count == 1)
                {
                    var m = eventMetrics.First();
                    result.Variations.Add(new ContextualExperimentVariationResultDto
                    {
                        VariationId = m.VariationId,
                        Exposures = m.TotalExposures,
                        Conversions = m.TotalConversions,
                        TotalValue = m.TotalValue,
                        ProbabilityToBeatBaseline = 1.0,
                        ExpectedUplift = 0,
                        RolloutWeight = state.ContextualRollouts?
                            .FirstOrDefault(x => 
                                x.ContextSlice == slice)?
                            .Rollout
                            .FirstOrDefault(x => 
                                x.VariationId == m.VariationId)?
                            .Weight ?? 0
                    });
                }

                contextualResults.Add(result);
            }
        }

        var experimentDuration = 
            DateTimeOffset.UtcNow - (state.ExperimentStartedAt 
                                     ?? DateTimeOffset.UtcNow.AddDays(-1));
        var timeSeriesSnapshot = new Dictionary<string, List<object>>();

        try
        {
            foreach (var evt in events)
            {
                var rawData = 
                    await _queryEngine.GetExperimentTimeSeriesAsync(
                        environmentId, flagKey, evt, experimentDuration, ct);

                timeSeriesSnapshot[evt] = rawData
                    .Select(object (x) => new
                {
                    Time = x.TimeBucket.ToString("o"),
                    x.VariationId,
                    x.Exposures,
                    x.Conversions,
                    ConversionRate = x.Exposures > 0 ? (double)x.Conversions / x.Exposures : 0
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture time series snapshot for {FlagKey}", flagKey);
            timeSeriesSnapshot.Clear();
        }

        var snapshotObj = new {
            Global = results,
            Contextual = contextualResults,
            TimeSeries = timeSeriesSnapshot
        };

        var snapshotJson = JsonSerializer.Serialize(snapshotObj);

        var configSnapshotObj = new {
            state.IsEnabled,
            state.FallthroughRollout,
            state.IsMabEnabled,
            state.MabGoalEvent,
            state.MabOptimizationType,
            state.MabExplorationFloor,
            ContextualRollouts = state.ContextualRollouts?.Select(cr => new {
                cr.ContextSlice,
                cr.Rollout
            }).ToList(),
            state.ContextPartitionKeys,
            Rules = state.Rules.Select(r => new {
                r.GroupId,
                r.Attribute,
                r.Operator,
                r.Value
            }).ToList()
        };
        var configSnapshotJson = JsonSerializer.Serialize(configSnapshotObj);
        
        return new ExperimentSnapshots(snapshotJson, configSnapshotJson);
    }
}
