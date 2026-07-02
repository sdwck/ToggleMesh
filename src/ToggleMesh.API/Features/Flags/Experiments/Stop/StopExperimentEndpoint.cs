using System.Text.Json;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Analytics.GetContextualExperimentDetails;
using ToggleMesh.API.Features.Analytics.GetExperimentDetails;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.Experiments.Stop;

public class StopExperimentEndpoint : ToggleEndpointWithoutRequest<GetFlagResponse>
{
    private readonly AppDbContext _db;
    private readonly ExperimentSnapshotBuilder _snapshotBuilder;

    public StopExperimentEndpoint(
        AppDbContext db,
        ExperimentSnapshotBuilder snapshotBuilder)
    {
        _db = db;
        _snapshotBuilder = snapshotBuilder;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/environments/{environmentId}/flags/{key}/experiments/stop");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("key")!;

        var state = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
            .Include(x => x.Rules)
            .Include(x => x.ContextualRollouts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.EnvironmentId == environmentId && x.FeatureFlag.Key == flagKey, ct);

        if (state is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!state.IsExperimentActive)
            ThrowError("Experiment is not active.");

        var snapshot = await _snapshotBuilder.BuildSnapshotAsync(environmentId, flagKey, state, ct);

        var iteration = new ExperimentIteration
        {
            EnvironmentId = environmentId,
            FlagKey = flagKey,
            StartedAt = state.ExperimentStartedAt ?? DateTimeOffset.UtcNow,
            EndedAt = DateTimeOffset.UtcNow,
            FinalMetricsSnapshot = snapshot.FinalMetricsSnapshot,
            FlagConfigSnapshot = snapshot.FlagConfigSnapshot
        };

        _db.ExperimentIterations.Add(iteration);

        state.IsExperimentActive = false;
        state.IsMabEnabled = false;

        await _db.SaveChangesAsync(ct);

        var response = state.ToDto();
        await new NotifyFlagUpdatedCommand(environmentId, flagKey, response).ExecuteAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
