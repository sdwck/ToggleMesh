using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.GetAll;

public class GetFlagsEndpoint : Endpoint<EmptyRequest, List<GetFlagResponse>>
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

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var flags = await _db.FeatureFlags
            .AsNoTracking()
            .Select(x => new GetFlagResponse(x.Key, x.IsEnabled))
            .ToListAsync(ct);
        
        await Send.OkAsync(flags, ct);
    }
}