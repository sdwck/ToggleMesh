using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Analytics.GetExperimentTimeSeries;

public class
    GetExperimentTimeSeriesEndpoint : ToggleEndpoint<GetExperimentTimeSeriesRequest, List<TimeSeriesResponsePoint>>
{
    private readonly IAnalyticsQueryEngine _queryEngine;
    private readonly AppDbContext _db;

    public GetExperimentTimeSeriesEndpoint(IAnalyticsQueryEngine queryEngine, AppDbContext db)
    {
        _queryEngine = queryEngine;
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/environments/{environmentId}/flags/{flagKey}/experiments/{eventName}/timeseries");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsView);
    }

    public override async Task HandleAsync(GetExperimentTimeSeriesRequest req, CancellationToken ct)
    {
        var state = await _db.FlagEnvironmentStates.FirstOrDefaultAsync(
            f => f.EnvironmentId == req.EnvironmentId && f.FeatureFlag.Key == req.FlagKey,
            ct);

        if (state?.ExperimentStartedAt == null)
        {
            await Send.OkAsync([], ct);
            return;
        }

        var durationSinceStart = 
            DateTimeOffset.UtcNow - state.ExperimentStartedAt.Value;
        var requestedDuration = TimeSpan.FromHours(req.Hours);
        var actualDuration = durationSinceStart < requestedDuration 
            ? durationSinceStart 
            : requestedDuration;

        var rawData =
            await _queryEngine.GetExperimentTimeSeriesAsync(
                req.EnvironmentId,
                req.FlagKey,
                req.EventName,
                actualDuration,
                ct);

        var result = rawData.Select(x => new TimeSeriesResponsePoint(
            x.TimeBucket.ToString("o"),
            x.VariationId,
            x.Exposures,
            x.Conversions,
            x.Exposures > 0 ? (double)x.Conversions / x.Exposures : 0
        )).ToList();

        await Send.OkAsync(result, ct);
    }
}
