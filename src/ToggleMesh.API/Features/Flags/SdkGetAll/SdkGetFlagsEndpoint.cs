using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.SdkGetAll;

public class SdkGetFlagsEndpoint : ToggleEndpoint<SdkGetFlagsRequest, List<GetFlagResponse>>
{
    private readonly AppDbContext _db;

    public SdkGetFlagsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/sdk/flags");
        Version(1);
        AllowAnonymous();
        PreProcessor<ApiKeyPreProcessor<SdkGetFlagsRequest>>();
    }

    public override async Task HandleAsync(SdkGetFlagsRequest req, CancellationToken ct)
    {
        var states = await _db.FlagEnvironmentStates
            .AsNoTracking()
            .Include(x => x.FeatureFlag)
            .Include(x => x.Rules)
            .Where(x => x.EnvironmentId == req.EnvId)
            .Select(state => new GetFlagResponse(
                state.FeatureFlag.Key, 
                state.IsEnabled, 
                state.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
                state.RolloutPercentage,
                state.TrueCount,
                state.FalseCount))
            .ToListAsync(ct);

        await Send.OkAsync(states, ct);
    }
}