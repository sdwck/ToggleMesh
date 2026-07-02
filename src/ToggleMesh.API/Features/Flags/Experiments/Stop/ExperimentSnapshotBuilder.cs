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
            .Where(x => x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ToListAsync(ct);

        var events = metrics.Select(x => x.EventName).Distinct().ToList();
        var results = new List<ExperimentResultDto>();

        foreach (var evt in events)
        {
            var control = metrics.FirstOrDefault(x => x.EventName == evt && !x.Variant);
            var treatment = metrics.FirstOrDefault(x => x.EventName == evt && x.Variant);

            if (control == null || treatment == null) continue;

            var result = new ExperimentResultDto
            {
                EventName = evt,
                ControlExposures = control.TotalExposures,
                ControlConversions = control.TotalConversions,
                TreatmentExposures = treatment.TotalExposures,
                TreatmentConversions = treatment.TotalConversions,
                ControlTotalValue = control.TotalValue,
                TreatmentTotalValue = treatment.TotalValue,
                LastCalculatedAt = control.LastCalculatedAt > treatment.LastCalculatedAt ? control.LastCalculatedAt : treatment.LastCalculatedAt,
                ExpectedUplift = _math.CalculateExpectedUplift(
                    control.TotalExposures, control.TotalConversions,
                    treatment.TotalExposures, treatment.TotalConversions),
                ProbabilityToBeatBaseline = _math.CalculateProbabilityBBeatsA(
                    control.TotalExposures, control.TotalConversions,
                    treatment.TotalExposures, treatment.TotalConversions)
            };

            results.Add(result);
        }

        var contextualMetrics = await _db.ContextualExperimentMetrics
            .AsNoTracking()
            .Where(x => x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ToListAsync(ct);

        var slices = contextualMetrics.Select(x => x.ContextSlice).Distinct();
        var contextualResults = new List<ContextualExperimentResultDto>();

        foreach (var slice in slices)
        {
            var sliceEvents = contextualMetrics
                    .Where(x => x.ContextSlice == slice)
                    .Select(x => x.EventName).Distinct();

            foreach (var evt in sliceEvents)
            {
                var control = contextualMetrics.FirstOrDefault(x => x.ContextSlice == slice && x.EventName == evt && !x.Variant);
                var treatment = contextualMetrics.FirstOrDefault(x => x.ContextSlice == slice && x.EventName == evt && x.Variant);

                if (control == null || treatment == null) continue;

                int? currentRollout = null;
                var isAutoManaged = true;
                var r = state.ContextualRollouts?
                    .FirstOrDefault(x => x.ContextSlice == slice);
                
                if (r != null)
                {
                    currentRollout = r.RolloutPercentage;
                    isAutoManaged = r.IsAutoManaged;
                }

                var result = new ContextualExperimentResultDto
                {
                    ContextSlice = slice,
                    EventName = evt,
                    ControlExposures = control.TotalExposures,
                    ControlConversions = control.TotalConversions,
                    TreatmentExposures = treatment.TotalExposures,
                    TreatmentConversions = treatment.TotalConversions,
                    ControlTotalValue = control.TotalValue,
                    TreatmentTotalValue = treatment.TotalValue,
                    LastCalculatedAt = control.LastCalculatedAt > treatment.LastCalculatedAt ? control.LastCalculatedAt : treatment.LastCalculatedAt,
                    CurrentRollout = currentRollout,
                    IsAutoManaged = isAutoManaged,
                    ExpectedUplift = _math.CalculateExpectedUplift(
                        control.TotalExposures, control.TotalConversions,
                        treatment.TotalExposures, treatment.TotalConversions),
                    ProbabilityToBeatBaseline = _math.CalculateProbabilityBBeatsA(
                        control.TotalExposures, control.TotalConversions,
                        treatment.TotalExposures, treatment.TotalConversions)
                };

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
                    x.Variant,
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
            state.RolloutPercentage,
            state.IsMabEnabled,
            state.MabGoalEvent,
            state.MabOptimizationType,
            ContextualRollouts = state.ContextualRollouts?.Select(cr => new {
                cr.ContextSlice,
                cr.RolloutPercentage
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
