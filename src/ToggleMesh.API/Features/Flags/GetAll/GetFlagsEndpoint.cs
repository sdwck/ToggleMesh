using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.GetAll;

public class GetFlagsEndpoint : Endpoint<GetFlagRequest, List<GetFlagResponse>>
{
    private readonly AppDbContext _db;

    public GetFlagsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/flags");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetFlagRequest req, CancellationToken ct)
    {
        var flags = await _db.FeatureFlags
            .AsNoTracking()
            .Include(x => x.Rules)
            .Where(x => x.EnvironmentId == req.EnvironmentId)
            .Select(x => new GetFlagResponse(
                x.Key, 
                x.IsEnabled, 
                x.Rules.Select(r => new RuleDto(r.Attribute, r.Operator, r.Value)),
                x.RolloutPercentage))
            .ToListAsync(ct);
        
        await Send.OkAsync(flags, ct);
    }
}