using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class MabTrafficShifterService : IMabTrafficShifterService
{
    private readonly ILogger<MabTrafficShifterService> _logger;

    public MabTrafficShifterService(ILogger<MabTrafficShifterService> logger)
    {
        _logger = logger;
    }

    public async Task ProcessMabTrafficShiftingAsync(
        AppDbContext db,
        BayesianMathService math,
        NotifyFlagUpdatedCommandHandler notifyHandler,
        CancellationToken ct)
    {
        var stateIds = await db.FlagEnvironmentStates
            .AsNoTracking()
            .Where(x => x.IsEnabled && x.IsMabEnabled && x.IsExperimentActive && x.RolloutPercentage.HasValue)
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (stateIds.Count == 0)
            return;

        foreach (var chunk in stateIds.Chunk(100))
        {
            var states = await db.FlagEnvironmentStates
                .Include(x => x.FeatureFlag)
                .Include(x => x.Rules)
                .Include(x => x.ContextualRollouts)
                .Where(x => chunk.Contains(x.Id))
                .AsSplitQuery()
                .ToListAsync(ct);

            var statesToNotify = new List<FlagEnvironmentState>();

            foreach (var state in states)
            {
                if (!state.IsExperimentActive || !state.IsMabEnabled) continue;

                var metrics = await db.ExperimentMetrics
                    .Where(x => x.EnvironmentId == state.EnvironmentId && x.FlagKey == state.FeatureFlag.Key)
                    .ToListAsync(ct);

                var control = metrics.FirstOrDefault(x => !x.Variant && x.EventName == state.MabGoalEvent);
                var treatment = metrics.FirstOrDefault(x => x.Variant && x.EventName == state.MabGoalEvent);

                if (control == null || treatment == null) continue;
                if (control.TotalExposures < 50 || treatment.TotalExposures < 50) continue;

                double probBBeatsA = math.CalculateProbabilityBBeatsA(
                    control.TotalExposures, control.TotalConversions,
                    treatment.TotalExposures, treatment.TotalConversions);

                int targetRollout = (int)Math.Round(probBBeatsA * 100);
                targetRollout = Math.Clamp(targetRollout, 5, 95);

                int maxStep = 10;
                int currentRollout = state.RolloutPercentage ?? 50;
                int newRollout = targetRollout;

                if (newRollout > currentRollout + maxStep) newRollout = currentRollout + maxStep;
                if (newRollout < currentRollout - maxStep) newRollout = currentRollout - maxStep;

                if (state.RolloutPercentage != newRollout)
                {
                    _logger.LogInformation(
                        "[MAB] Shifting traffic for Flag {Flag} in Env {Env}: {Old}% -> {New}% (Prob: {Prob})",
                        state.FeatureFlag.Key, state.EnvironmentId, state.RolloutPercentage, newRollout, probBBeatsA);

                    await db.FlagEnvironmentStates
                        .Where(x => x.Id == state.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.RolloutPercentage, newRollout), ct);

                    state.RolloutPercentage = newRollout;
                    statesToNotify.Add(state);
                }
            }

            if (statesToNotify.Count > 0)
            {
                foreach (var state in statesToNotify)
                {
                    var response = state.ToDto();
                    await notifyHandler.ExecuteAsync(
                        new NotifyFlagUpdatedCommand(state.EnvironmentId, state.FeatureFlag.Key, response), ct);
                }
            }
        }
    }

    public async Task ProcessContextualBanditAutoSegmentationAsync(
        AppDbContext db,
        BayesianMathService math,
        NotifyFlagUpdatedCommandHandler notifyHandler,
        CancellationToken ct)
    {
        var stateIds = await db.FlagEnvironmentStates
            .AsNoTracking()
            .Where(x => x.IsEnabled && x.IsMabEnabled && x.IsExperimentActive && x.ContextPartitionKeys.Length > 0)
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (stateIds.Count == 0)
            return;

        foreach (var chunk in stateIds.Chunk(100))
        {
            var states = await db.FlagEnvironmentStates
                .Include(x => x.FeatureFlag)
                .Include(x => x.Rules)
                .Include(x => x.ContextualRollouts)
                .Where(x => chunk.Contains(x.Id))
                .AsSplitQuery()
                .ToListAsync(ct);

            var statesToNotify = new List<FlagEnvironmentState>();

            foreach (var state in states)
            {
                var metrics = await db.ContextualExperimentMetrics
                    .Where(x => x.EnvironmentId == state.EnvironmentId && x.FlagKey == state.FeatureFlag.Key)
                    .ToListAsync(ct);

                var slices = metrics.Select(x => x.ContextSlice).Distinct().ToList();
                var hasChanges = false;

                foreach (var slice in slices)
                {
                    var control = metrics.FirstOrDefault(x => !x.Variant && x.EventName == state.MabGoalEvent && x.ContextSlice == slice);
                    var treatment = metrics.FirstOrDefault(x => x.Variant && x.EventName == state.MabGoalEvent && x.ContextSlice == slice);

                    if (control == null || treatment == null) continue;
                    if (control.TotalExposures < 50 || treatment.TotalExposures < 50) continue;

                    double probBBeatsA;
                    if (state.MabOptimizationType == MabOptimizationType.Conversion)
                        probBBeatsA = math.CalculateProbabilityBBeatsA(
                            control.TotalExposures, control.TotalConversions,
                            treatment.TotalExposures, treatment.TotalConversions);
                    else
                        probBBeatsA = math.CalculateProbabilityBBeatsA_Revenue(
                            control.TotalExposures, control.TotalValue, control.SumOfSquaredValues,
                            treatment.TotalExposures, treatment.TotalValue, treatment.SumOfSquaredValues);

                    int calculatedRollout = (int)Math.Round(probBBeatsA * 100);
                    calculatedRollout = Math.Clamp(calculatedRollout, 0, 100);

                    state.ContextualRollouts ??= new List<ContextualRollout>();

                    var existingRollout = state.ContextualRollouts.FirstOrDefault(r => r.ContextSlice == slice);

                    if (existingRollout != null && !existingRollout.IsAutoManaged)
                        continue;

                    if (existingRollout == null || existingRollout.RolloutPercentage != calculatedRollout)
                    {
                        if (existingRollout == null)
                        {
                            state.ContextualRollouts.Add(new ContextualRollout
                            {
                                ContextSlice = slice,
                                RolloutPercentage = calculatedRollout,
                                IsAutoManaged = true
                            });
                        }
                        else
                        {
                            existingRollout.RolloutPercentage = calculatedRollout;
                        }

                        _logger.LogInformation("[Contextual MAB] Flag {Flag} Context {Slice} rollout adjusted to {Rollout}%", state.FeatureFlag.Key, slice, calculatedRollout);
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    await db.SaveChangesAsync(ct);
                    statesToNotify.Add(state);
                }
            }

            if (statesToNotify.Count > 0)
            {
                foreach (var state in statesToNotify)
                {
                    var response = state.ToDto();
                    await notifyHandler.ExecuteAsync(new NotifyFlagUpdatedCommand(
                        state.EnvironmentId,
                        state.FeatureFlag.Key,
                        response
                    ), ct);
                }
            }
        }
    }
}
