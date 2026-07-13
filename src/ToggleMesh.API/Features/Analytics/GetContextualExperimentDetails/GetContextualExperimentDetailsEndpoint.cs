using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Analytics.GetContextualExperimentDetails;

public class GetContextualExperimentDetailsEndpoint : ToggleEndpointWithoutRequest<List<ContextualExperimentResultDto>>
{
    private readonly AppDbContext _db;
    private readonly BayesianMathService _math;

    public GetContextualExperimentDetailsEndpoint(AppDbContext db, BayesianMathService math)
    {
        _db = db;
        _math = math;
    }

    public override void Configure()
    {
        Get("/projects/{projectId:guid}/environments/{envId:guid}/flags/{key}/experiments/contextual");
        Version(1);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var envId = Route<Guid>("envId");
        var flagKey = Route<string>("key")!;

        var metrics = await _db.ContextualExperimentMetrics
            .Where(x => 
                x.EnvironmentId == envId && x.FlagKey == flagKey && 
                x.VariationId != Guid.Empty)
            .ToListAsync(ct);

        var state = await _db.FlagEnvironmentStates
            .AsNoTracking()
            .Include(x => x.ContextualRollouts)
            .FirstOrDefaultAsync(x => 
                x.EnvironmentId == envId &&
                x.FeatureFlag.Key == flagKey, ct);

        var slices = metrics
            .Select(x => x.ContextSlice)
            .Distinct();
        var results = new List<ContextualExperimentResultDto>();

        foreach (var slice in slices)
        {
            var events = metrics
                .Where(x => x.ContextSlice == slice)
                .Select(x => x.EventName)
                .Distinct()
                .ToList();

            foreach (var evt in events)
            {
                var eventMetrics = metrics
                    .Where(x => 
                        x.ContextSlice == slice && x.EventName == evt)
                    .ToList();
                if (eventMetrics.Count == 0) 
                    continue;

                var isAutoManaged = true;
                ContextualRollout? currentRollout = null;
                if (state?.ContextualRollouts != null)
                {
                    currentRollout = state.ContextualRollouts
                        .FirstOrDefault(x => x.ContextSlice == slice);
                    if (currentRollout != null)
                        isAutoManaged = currentRollout.IsAutoManaged;
                }

                var result = new ContextualExperimentResultDto
                {
                    ContextSlice = slice,
                    EventName = evt,
                    IsRevenueBased = state?.MabOptimizationType == MabOptimizationType.Revenue,
                    LastCalculatedAt = eventMetrics
                        .Max(m => m.LastCalculatedAt),
                    IsAutoManaged = isAutoManaged
                };

                var variations = eventMetrics
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
                        var m = eventMetrics.Where(x => x.VariationId == variations[i]).OrderByDescending(x => x.LastCalculatedAt).First();
                        exposures[i] = m.TotalExposures;
                        conversions[i] = m.TotalConversions;
                        values[i] = m.TotalValue;
                        sumSquared[i] = m.SumOfSquaredValues;
                    }

                    var probs = !result.IsRevenueBased 
                        ? _math.CalculateDirichletProbabilities(
                            exposures, 
                            conversions) 
                        : _math.CalculateDirichletProbabilities_Revenue(
                            exposures, 
                            values, 
                            sumSquared);
                    
                    var baselineIdx = 0;
                    long maxExp = -1;
                    for (var i = 0; i < variations.Count; i++)
                        if (exposures[i] > maxExp)
                        {
                            maxExp = exposures[i];
                            baselineIdx = i;
                        }

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

                        var weight = 0;
                        if (currentRollout != null)
                            weight = currentRollout.Rollout
                                .FirstOrDefault(w => 
                                    w.VariationId == variations[i])?.Weight ?? 0;
                        else if (state != null)
                            weight = state.FallthroughRollout
                                .FirstOrDefault(w => 
                                    w.VariationId == variations[i])?.Weight ?? 0;

                        result.Variations.Add(new ContextualExperimentVariationResultDto
                        {
                            VariationId = variations[i],
                            Exposures = exposures[i],
                            Conversions = conversions[i],
                            TotalValue = values[i],
                            ProbabilityToBeatBaseline = probs[i],
                            ExpectedUplift = uplift,
                            RolloutWeight = weight
                        });
                    }
                }
                else if (variations.Count == 1)
                {
                    var m = eventMetrics
                        .OrderByDescending(x => x.LastCalculatedAt)
                        .First();
                    
                    var weight = 0;
                    if (currentRollout != null)
                        weight = currentRollout.Rollout
                            .FirstOrDefault(w => 
                                w.VariationId == m.VariationId)?.Weight ?? 0;
                    else if (state != null)
                        weight = state.FallthroughRollout
                            .FirstOrDefault(w => 
                                w.VariationId == m.VariationId)?.Weight ?? 0;
                    
                    result.Variations.Add(new ContextualExperimentVariationResultDto
                    {
                        VariationId = m.VariationId,
                        Exposures = m.TotalExposures,
                        Conversions = m.TotalConversions,
                        TotalValue = m.TotalValue,
                        ProbabilityToBeatBaseline = 1.0,
                        ExpectedUplift = 0,
                        RolloutWeight = weight
                    });
                }

                results.Add(result);
            }
        }

        await Send.OkAsync(results, ct);
    }
}
