using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.SdkGetAll;

public class SdkGetFlagsEndpoint : Endpoint<SdkGetFlagsRequest, List<GetFlagResponse>>
{
    private readonly AppDbContext _db;

    public SdkGetFlagsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/sdk/flags");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SdkGetFlagsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ApiKey))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var envKey = await _db.EnvironmentKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApiKey == req.ApiKey && (x.ExpireOn == null || x.ExpireOn > DateTime.UtcNow), ct);

        if (envKey == null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }
        
        var flags = await _db.FeatureFlags
            .AsNoTracking()
            .Include(x => x.Rules)
            .Where(x => x.EnvironmentId == envKey.EnvironmentId)
            .Select(x => new GetFlagResponse(
                x.Key, 
                x.IsEnabled, 
                x.Rules.Select(r => new RuleDto(r.Attribute, r.Operator, r.Value)),
                x.RolloutPercentage))
            .ToListAsync(ct);
        
        await Send.OkAsync(flags, ct);
    }
}