using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Features.Analytics.GetExperimentDetails;

public class GetExperimentDetailsEndpoint : ToggleEndpointWithoutRequest<List<ExperimentResultDto>>
{
    private readonly AppDbContext _db;
    private readonly BayesianMathService _math;

    public GetExperimentDetailsEndpoint(AppDbContext db, BayesianMathService math)
    {
        _db = db;
        _math = math;
    }

    public override void Configure()
    {
        Get("/projects/{projectId:guid}/environments/{envId:guid}/flags/{key}/experiments");
        Version(1);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var envId = Route<Guid>("envId");
        var flagKey = Route<string>("key")!;
        
        var metrics = await _db.ExperimentMetrics
            .Where(x => 
                x.EnvironmentId == envId 
                && x.FlagKey == flagKey 
                && x.VariationId != Guid.Empty)
            .ToListAsync(ct);

        var state = await _db.FlagEnvironmentStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => 
                x.EnvironmentId == envId && 
                x.FeatureFlag.Key == flagKey, ct);

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
                IsRevenueBased = state?.MabOptimizationType == MabOptimizationType.Revenue,
                LastCalculatedAt = eventMetrics.Max(m => m.LastCalculatedAt)
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
                    var m = eventMetrics.First(x => x.VariationId == variations[i]);
                    exposures[i] = m.TotalExposures;
                    conversions[i] = m.TotalConversions;
                    values[i] = m.TotalValue;
                    sumSquared[i] = m.SumOfSquaredValues;
                }

                double[] probs;
                if (!result.IsRevenueBased)
                    probs = _math.CalculateDirichletProbabilities(exposures, conversions);
                else
                    probs = _math.CalculateDirichletProbabilities_Revenue(exposures, values, sumSquared);

                var baselineIdx = 0;
                long maxExp = -1;
                for (var i = 0; i < variations.Count; i++)
                {
                    if (exposures[i] <= maxExp) 
                        continue;
                    
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

                    result.Variations.Add(new ExperimentVariationResultDto
                    {
                        VariationId = variations[i],
                        Exposures = exposures[i],
                        Conversions = conversions[i],
                        TotalValue = values[i],
                        ProbabilityToBeatBaseline = probs[i],
                        ExpectedUplift = uplift
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

        await Send.OkAsync(results, ct);
    }
}
