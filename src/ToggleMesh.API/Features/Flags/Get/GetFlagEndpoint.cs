using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Get;

public class GetFlagEndpoint : Endpoint<EmptyRequest, GetFlagResponse>
{
    private readonly AppDbContext _db;

    public GetFlagEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/flags/{id:int}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var id = Route<int>("id");
        var flag = await _db.FeatureFlags
            .FindAsync([id], ct);

        if (flag is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        await Send.OkAsync(new GetFlagResponse(flag.Key, flag.IsEnabled), ct);
    }
}