using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

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
        var flagKey = Route<string>("key");

        var metrics = await _db.ContextualExperimentMetrics
            .Where(x => x.EnvironmentId == envId && x.FlagKey == flagKey)
            .ToListAsync(ct);

        var state = await _db.FlagEnvironmentStates
            .AsNoTracking()
            .Include(x => x.ContextualRollouts)
            .FirstOrDefaultAsync(x => x.EnvironmentId == envId && x.FeatureFlag.Key == flagKey, ct);

        var slices = metrics.Select(x => x.ContextSlice).Distinct();
        var results = new List<ContextualExperimentResultDto>();

        foreach (var slice in slices)
        {
            var events = metrics.Where(x => x.ContextSlice == slice).Select(x => x.EventName).Distinct();

            foreach (var evt in events)
            {
                var control = metrics.FirstOrDefault(x => x.ContextSlice == slice && x.EventName == evt && !x.Variant);
                var treatment = metrics.FirstOrDefault(x => x.ContextSlice == slice && x.EventName == evt && x.Variant);

                int? currentRollout = null;
                var isAutoManaged = true;
                if (state?.ContextualRollouts != null)
                {
                    var r = state.ContextualRollouts.FirstOrDefault(x => x.ContextSlice == slice);
                    if (r != null)
                    {
                        currentRollout = r.RolloutPercentage;
                        isAutoManaged = r.IsAutoManaged;
                    }
                }

                var result = new ContextualExperimentResultDto
                {
                    ContextSlice = slice,
                    EventName = evt,
                    ControlExposures = control?.TotalExposures ?? 0,
                    ControlConversions = control?.TotalConversions ?? 0,
                    TreatmentExposures = treatment?.TotalExposures ?? 0,
                    TreatmentConversions = treatment?.TotalConversions ?? 0,
                    ControlTotalValue = control?.TotalValue ?? 0,
                    TreatmentTotalValue = treatment?.TotalValue ?? 0,
                    LastCalculatedAt = control != null && treatment != null 
                        ? (control.LastCalculatedAt > treatment.LastCalculatedAt ? control.LastCalculatedAt : treatment.LastCalculatedAt)
                        : (control?.LastCalculatedAt ?? treatment?.LastCalculatedAt ?? DateTimeOffset.UtcNow),
                    CurrentRollout = currentRollout,
                    IsAutoManaged = isAutoManaged,
                    ExpectedUplift = _math.CalculateExpectedUplift(
                        control?.TotalExposures ?? 0, control?.TotalConversions ?? 0,
                        treatment?.TotalExposures ?? 0, treatment?.TotalConversions ?? 0),
                    ProbabilityToBeatBaseline = _math.CalculateProbabilityBBeatsA(
                        control?.TotalExposures ?? 0, control?.TotalConversions ?? 0,
                        treatment?.TotalExposures ?? 0, treatment?.TotalConversions ?? 0)
                };

                results.Add(result);
            }
        }

        await Send.OkAsync(results, ct);
    }
}
