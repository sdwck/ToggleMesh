using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.Experiments.Iterations.Delete;

public class DeleteExperimentIterationEndpoint : ToggleEndpointWithoutRequest
{
    private readonly AppDbContext _db;

    public DeleteExperimentIterationEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/projects/{projectId}/environments/{environmentId}/flags/{key}/experiments/iterations/{iterationId}");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("key");
        var iterationId = Route<Guid>("iterationId");

        var deleted = await _db.ExperimentIterations
            .Where(x => x.Id == iterationId && x.EnvironmentId == environmentId && x.FlagKey == flagKey)
            .ExecuteDeleteAsync(ct);

        if (deleted == 0)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
