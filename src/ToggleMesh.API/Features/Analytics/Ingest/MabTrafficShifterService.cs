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
        db.SystemActorEmail = "mab-automation@togglemesh.com";
        var stateIds = await db.FlagEnvironmentStates
            .AsNoTracking()
            .Where(x => 
                x.IsEnabled && x.IsMabEnabled && x.IsExperimentActive)
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (stateIds.Count == 0)
            return;

        foreach (var chunk in stateIds.Chunk(100))
        {
            var states = await db.FlagEnvironmentStates
                .Include(x => x.FeatureFlag)
                    .ThenInclude(x => x.Variations)
                .Include(x => x.Rules)
                .Include(x => x.ContextualRollouts)
                .Where(x => chunk.Contains(x.Id))
                .AsSplitQuery()
                .ToListAsync(ct);

            var statesToNotify = new List<FlagEnvironmentState>();

            foreach (var state in states)
            {
                if (!state.IsExperimentActive 
                    || !state.IsMabEnabled 
                    || state.FallthroughRollout.Count == 0) 
                    continue;

                var metrics = await db.ExperimentMetrics
                    .Where(x => 
                        x.EnvironmentId == state.EnvironmentId 
                        && x.FlagKey == state.FeatureFlag.Key 
                        && x.EventName == state.MabGoalEvent)
                    .ToListAsync(ct);
                
                var variations = state.FeatureFlag.Variations
                    .Select(v => v.Id)
                    .ToList();
                if (state.OffVariationId != null && 
                    !variations.Contains(state.OffVariationId.Value))
                    variations.Add(state.OffVariationId.Value);

                if (variations.Count < 2) 
                    continue;

                var exposures = new long[variations.Count];
                var conversions = new long[variations.Count];
                var values = new double[variations.Count];
                var sumSquared = new double[variations.Count];
                
                var validIndices = new List<int>();
                for (var i = 0; i < variations.Count; i++)
                {
                    var metric = metrics
                        .Where(x => x.VariationId == variations[i])
                        .OrderByDescending(x => x.LastCalculatedAt)
                        .FirstOrDefault();
                    exposures[i] = metric?.TotalExposures ?? 0;
                    conversions[i] = metric?.TotalConversions ?? 0;
                    values[i] = metric?.TotalValue ?? 0;
                    sumSquared[i] = metric?.SumOfSquaredValues ?? 0;
                    
                    if (exposures[i] >= 50) 
                        validIndices.Add(i);
                }

                if (validIndices.Count == 0) continue;

                double[] probs;
                if (state.MabOptimizationType == MabOptimizationType.Conversion)
                    probs = math.CalculateDirichletProbabilities(exposures, conversions);
                else
                    probs = math.CalculateDirichletProbabilities_Revenue(
                        exposures, values, sumSquared);

                double validProbsSum = 0;
                for (var i = 0; i < variations.Count; i++)
                {
                    if (!validIndices.Contains(i)) 
                        probs[i] = 0;
                    validProbsSum += probs[i];
                }

                if (validProbsSum > 0)
                    for (var i = 0; i < variations.Count; i++) 
                        probs[i] /= validProbsSum;
                else
                    for (var i = 0; i < variations.Count; i++) 
                        probs[i] = validIndices.Contains(i) ? 1.0 / validIndices.Count : 0;

                var changed = false;
                
                var floor = state.MabExplorationFloor / 100.0;
                var n = variations.Count;
                if (floor * n > 1.0) 
                    floor = 1.0 / n;

                var newRollout = new List<VariationWeight>();
                var remainingWeight = 10000;
                
                for (var i = 0; i < n; i++)
                {
                    if (i == n - 1)
                        newRollout.Add(new VariationWeight
                        {
                            VariationId = variations[i], 
                            Weight = remainingWeight
                        });
                    else
                    {
                        var newP = floor + probs[i] * (1.0 - n * floor);
                        var weight = (int)Math.Round(newP * 10000);
                        if (weight > remainingWeight) 
                            weight = remainingWeight;
                        newRollout.Add(new VariationWeight { VariationId = variations[i], Weight = weight });
                        remainingWeight -= weight;
                    }
                }

                var shouldUpdate = state.FallthroughRollout.Count != newRollout.Count;
                if (!shouldUpdate)
                {
                    foreach (var r in state.FallthroughRollout)
                    {
                        var nr = newRollout
                            .FirstOrDefault(x => x.VariationId == r.VariationId);
                        if (nr != null && Math.Abs(nr.Weight - r.Weight) <= 50) 
                            continue;
                        
                        shouldUpdate = true;
                        break;
                    }
                }

                if (shouldUpdate)
                {
                    state.FallthroughRollout.Clear();
                    foreach (var item in newRollout) 
                        state.FallthroughRollout.Add(item);
                    changed = true;
                    
                    _logger.LogInformation(
                        "[MAB] Shifting traffic for Flag {Flag} in Env {Env}",
                        state.FeatureFlag.Key,
                        state.EnvironmentId);
                }

                if (!changed) 
                    continue;
                
                try
                {
                    db.FlagEnvironmentStates.Update(state);
                    await db.SaveChangesAsync(ct);
                    statesToNotify.Add(state);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("[MAB] Concurrency conflict detected while shifting traffic for Flag {Flag} in Env {Env}. Discarding update.", state.FeatureFlag.Key, state.EnvironmentId);
                    db.Entry(state).State = EntityState.Detached;
                }
            }

            if (statesToNotify.Count <= 0) 
                continue;
            
            foreach (var state in statesToNotify)
            {
                var response = state.ToDto();
                await notifyHandler.ExecuteAsync(
                    new NotifyFlagUpdatedCommand(
                        state.EnvironmentId, 
                        state.FeatureFlag.Key, 
                        response, 
                        state.ToSdkDto()), 
                    ct);
            }
        }
    }

    public async Task ProcessContextualBanditAutoSegmentationAsync(
        AppDbContext db,
        BayesianMathService math,
        NotifyFlagUpdatedCommandHandler notifyHandler,
        CancellationToken ct)
    {
        db.SystemActorEmail = "mab-automation@togglemesh.com";
        var stateIds = await db.FlagEnvironmentStates
            .AsNoTracking()
            .Where(x => x.IsEnabled && x.IsMabEnabled && x.IsExperimentActive)
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (stateIds.Count == 0)
            return;

        foreach (var chunk in stateIds.Chunk(100))
        {
            var states = await db.FlagEnvironmentStates
                .Include(x => x.FeatureFlag)
                    .ThenInclude(x => x.Variations)
                .Include(x => x.Rules)
                .Include(x => x.ContextualRollouts)
                .Where(x => chunk.Contains(x.Id))
                .AsSplitQuery()
                .ToListAsync(ct);

            var statesToNotify = new List<FlagEnvironmentState>();

            foreach (var state in states)
            {
                var metrics = await db.ContextualExperimentMetrics
                    .Where(x => x.EnvironmentId == state.EnvironmentId && x.FlagKey == state.FeatureFlag.Key && x.EventName == state.MabGoalEvent)
                    .ToListAsync(ct);

                var slices = metrics.Select(x => x.ContextSlice).Distinct().ToList();
                var hasChanges = false;
                
                if (state.ContextPartitionKeys.Length == 0) 
                    continue;
                
                var variations = state.FeatureFlag.Variations.Select(v => v.Id).ToList();
                if (state.OffVariationId != null && !variations.Contains(state.OffVariationId.Value))
                    variations.Add(state.OffVariationId.Value);

                if (variations.Count < 2) 
                    continue;

                foreach (var slice in slices)
                {
                    var exposures = new long[variations.Count];
                    var conversions = new long[variations.Count];
                    var values = new double[variations.Count];
                    var sumSquared = new double[variations.Count];
                    
                    var validIndices = new List<int>();
                    for (var i = 0; i < variations.Count; i++)
                    {
                        var metric = metrics.Where(x => x.VariationId == variations[i] && x.ContextSlice == slice).OrderByDescending(x => x.LastCalculatedAt).FirstOrDefault();
                        exposures[i] = metric?.TotalExposures ?? 0;
                        conversions[i] = metric?.TotalConversions ?? 0;
                        values[i] = metric?.TotalValue ?? 0;
                        sumSquared[i] = metric?.SumOfSquaredValues ?? 0;
                        
                        if (exposures[i] >= 50) 
                            validIndices.Add(i);
                    }

                    if (validIndices.Count == 0) 
                        continue;

                    var probs = state.MabOptimizationType == MabOptimizationType.Conversion 
                        ? math.CalculateDirichletProbabilities(exposures, conversions) 
                        : math.CalculateDirichletProbabilities_Revenue(
                            exposures, values, sumSquared);

                    double validProbsSum = 0;
                    for (var i = 0; i < variations.Count; i++)
                    {
                        if (!validIndices.Contains(i)) probs[i] = 0;
                        validProbsSum += probs[i];
                    }

                    if (validProbsSum > 0)
                        for (var i = 0; i < variations.Count; i++) probs[i] /= validProbsSum;
                    else
                        for (var i = 0; i < variations.Count; i++) 
                            probs[i] = validIndices.Contains(i) 
                                ? 1.0 / validIndices.Count 
                                : 0;
                    
                    var floor = state.MabExplorationFloor / 100.0;
                    var n = variations.Count;
                    if (floor * n > 1.0) floor = 1.0 / n;

                    var newRollout = new List<VariationWeight>();
                    var remainingWeight = 10000;
                    
                    for (var i = 0; i < n; i++)
                    {
                        if (i == n - 1)
                            newRollout.Add(
                                new VariationWeight
                                {
                                    VariationId = variations[i], 
                                    Weight = remainingWeight
                                });
                        else
                        {
                            var newP = floor + probs[i] * (1.0 - n * floor);
                            var weight = (int)Math.Round(newP * 10000);
                            if (weight > remainingWeight) weight = remainingWeight;
                            newRollout.Add(new VariationWeight { VariationId = variations[i], Weight = weight });
                            remainingWeight -= weight;
                        }
                    }

                    var existingRollout = state.ContextualRollouts
                        .FirstOrDefault(r => r.ContextSlice == slice);

                    if (existingRollout is { IsAutoManaged: false })
                        continue;

                    var shouldUpdate = existingRollout == null || 
                                       existingRollout.Rollout.Count != newRollout.Count;
                    if (!shouldUpdate)
                        foreach (var r in existingRollout!.Rollout)
                        {
                            var nr = newRollout.FirstOrDefault(x => x.VariationId == r.VariationId);
                            if (nr != null && Math.Abs(nr.Weight - r.Weight) <= 50) 
                                continue;
                            
                            shouldUpdate = true;
                            break;
                        }

                    if (!shouldUpdate) 
                        continue;
                    
                    if (existingRollout != null)
                    {
                        state.ContextualRollouts.Remove(existingRollout);
                        db.ContextualRollouts.Remove(existingRollout);
                    }

                    state.ContextualRollouts.Add(new ContextualRollout
                    {
                        FlagEnvironmentStateId = state.Id,
                        ContextSlice = slice,
                        Rollout = newRollout,
                        IsAutoManaged = true
                    });

                    _logger.LogDebug("[Contextual MAB] Flag {Flag} Context {Slice} rollout adjusted", state.FeatureFlag.Key, slice);
                    hasChanges = true;
                }

                if (!hasChanges) 
                    continue;
                
                try
                {
                    db.FlagEnvironmentStates.Update(state);
                    await db.SaveChangesAsync(ct);
                    statesToNotify.Add(state);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("[Contextual MAB] Concurrency conflict detected while shifting traffic for Flag {Flag} in Env {Env}. Discarding update.", state.FeatureFlag.Key, state.EnvironmentId);
                    db.Entry(state).State = EntityState.Detached;
                }
            }

            if (statesToNotify.Count <= 0) 
                continue;
            
            foreach (var state in statesToNotify)
            {
                var response = state.ToDto();
                await notifyHandler.ExecuteAsync(new NotifyFlagUpdatedCommand(
                    state.EnvironmentId,
                    state.FeatureFlag.Key,
                    response,
                    state.ToSdkDto()
                ), ct);
            }
        }
    }
}
