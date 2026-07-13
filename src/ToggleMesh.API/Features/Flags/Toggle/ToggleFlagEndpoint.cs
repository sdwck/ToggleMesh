using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.Toggle;

public class ToggleFlagEndpoint : ToggleEndpoint<ToggleFlagRequest>
{
    private readonly AppDbContext _db;
    
    public ToggleFlagEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/environments/{environmentId}/flags/{key}/toggle");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsToggle);
    }

    public override async Task HandleAsync(ToggleFlagRequest req, CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("key")!;

        var state = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
                .ThenInclude(f => f.Variations)
            .Include(x => x.Rules)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.EnvironmentId == environmentId && x.FeatureFlag.Key == flagKey, ct);

        if (state is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        state.IsEnabled = req.IsEnabled;
        await _db.SaveChangesAsync(ct);

        var response = state.ToDto();
        await new NotifyFlagUpdatedCommand(
            environmentId, 
            flagKey, 
            response,
            state.ToSdkDto()
        ).ExecuteAsync(ct);

        await Send.OkAsync(new { flagKey, req.IsEnabled }, ct);
    }
}
