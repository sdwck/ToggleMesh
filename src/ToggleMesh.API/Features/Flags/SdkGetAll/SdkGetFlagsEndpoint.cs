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
        var flags = await _db.FeatureFlags
            .AsNoTracking()
            .Include(x => x.Rules)
            .Where(x => x.EnvironmentId == req.EnvId)
            .Select(x => new GetFlagResponse(
                x.Key, 
                x.IsEnabled, 
                x.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
                x.RolloutPercentage,
                x.TrueCount,
                x.FalseCount))
            .ToListAsync(ct);

        await Send.OkAsync(flags, ct);
    }
}