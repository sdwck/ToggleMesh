using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.SetContextualRollout;

public class SetContextualRolloutEndpoint : ToggleEndpoint<SetContextualRolloutRequest, GetFlagResponse>
{
    private readonly AppDbContext _db;
    public SetContextualRolloutEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/environments/{environmentId}/flags/{key}/contextual-rollouts/set");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(SetContextualRolloutRequest req, CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("key")!;

        var state = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
                .ThenInclude(x => x.Variations)
            .Include(x => x.Rules)
            .Include(x => x.ContextualRollouts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.EnvironmentId == environmentId && x.FeatureFlag.Key == flagKey, ct);

        if (state is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (req.Rollout == null || req.Rollout.Sum(r => r.Weight) > 10000)
            ThrowError("Rollout weights must not sum to more than 10000.");

        var rollout = state.ContextualRollouts.FirstOrDefault(x => x.ContextSlice == req.ContextSlice);
        if (rollout == null)
        {
            rollout = new ContextualRollout
            {
                ContextSlice = req.ContextSlice,
                Rollout = req.Rollout,
                IsAutoManaged = false
            };
            state.ContextualRollouts.Add(rollout);
        }
        else
        {
            rollout.Rollout = req.Rollout;
            rollout.IsAutoManaged = false;
        }

        await _db.SaveChangesAsync(ct);

        var response = state.ToDto();
        await new NotifyFlagUpdatedCommand(
            environmentId, 
            flagKey, 
            response,
            state.ToSdkDto()
        ).ExecuteAsync(ct);

        await Send.OkAsync(response, ct);
    }
}
