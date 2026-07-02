using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;

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
        var flagKey = Route<string>("key");
        
        var metrics = await _db.ExperimentMetrics
            .Where(x => x.EnvironmentId == envId && x.FlagKey == flagKey)
            .ToListAsync(ct);

        var events = metrics.Select(x => x.EventName).Distinct();

        var results = new List<ExperimentResultDto>();

        foreach (var evt in events)
        {
            var control = metrics.FirstOrDefault(x => x.EventName == evt && !x.Variant);
            var treatment = metrics.FirstOrDefault(x => x.EventName == evt && x.Variant);

            var result = new ExperimentResultDto
            {
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
                ExpectedUplift = _math.CalculateExpectedUplift(
                    control?.TotalExposures ?? 0, control?.TotalConversions ?? 0,
                    treatment?.TotalExposures ?? 0, treatment?.TotalConversions ?? 0),
                ProbabilityToBeatBaseline = _math.CalculateProbabilityBBeatsA(
                    control?.TotalExposures ?? 0, control?.TotalConversions ?? 0,
                    treatment?.TotalExposures ?? 0, treatment?.TotalConversions ?? 0)
            };

            results.Add(result);
        }

        await Send.OkAsync(results, ct);
    }
}
