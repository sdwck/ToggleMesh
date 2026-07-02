using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.Experiments.Iterations.Get;

public class GetExperimentIterationsEndpoint : ToggleEndpointWithoutRequest<List<ExperimentIterationDto>>
{
    private readonly AppDbContext _db;

    public GetExperimentIterationsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/projects/{projectId}/environments/{environmentId}/flags/{key}/experiments/iterations");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsView);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("key");

        var iterations = await _db.ExperimentIterations
            .AsNoTracking()
            .Where(x => x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .OrderByDescending(x => x.EndedAt)
            .Select(x => new ExperimentIterationDto(
                x.Id,
                x.EnvironmentId,
                x.FlagKey,
                x.StartedAt,
                x.EndedAt,
                x.FinalMetricsSnapshot,
                x.FlagConfigSnapshot))
            .ToListAsync(ct);

        await Send.OkAsync(iterations, ct);
    }
}
