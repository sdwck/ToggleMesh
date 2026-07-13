using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Analytics.DeleteContextualRollout;

public class DeleteContextualRolloutEndpoint : ToggleEndpoint<DeleteContextualRolloutRequest, GetFlagResponse>
{
    private readonly AppDbContext _db;
    public DeleteContextualRolloutEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Delete("/projects/{projectId:guid}/environments/{envId:guid}/flags/{key}/contextual-rollouts");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(DeleteContextualRolloutRequest req, CancellationToken ct)
    {
        var envId = Route<Guid>("envId");
        var flagKey = Route<string>("key")!;

        var state = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
                .ThenInclude(x => x.Variations)
            .Include(x => x.Rules)
            .Include(x => x.ContextualRollouts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.EnvironmentId == envId && x.FeatureFlag.Key == flagKey, ct);

        if (state is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var rollout = state.ContextualRollouts.FirstOrDefault(x => x.ContextSlice == req.ContextSlice);
        if (rollout != null)
            _db.ContextualRollouts.Remove(rollout);

        await _db.SaveChangesAsync(ct);

        var response = state.ToDto();
        await new NotifyFlagUpdatedCommand(
            envId, 
            flagKey, 
            response,
            state.ToSdkDto()
        ).ExecuteAsync(ct);

        await Send.OkAsync(response, ct);
    }
}
