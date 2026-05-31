using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Flags.GetAll;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Create;

public class CreateFlagEndpoint : Endpoint<CreateFlagRequest>
{
    private readonly AppDbContext _db;

    public CreateFlagEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/flags");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateFlagRequest req, CancellationToken ct)
    {
        var exists = await _db.FeatureFlags
            .AnyAsync(x => x.EnvironmentId == req.EnvironmentId && x.Key == req.Key, ct);
        if (exists)
        {
            AddError(x => x.Key, 
                "A feature flag with this key already exists.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var newFlag = new FeatureFlag
        {
            EnvironmentId = req.EnvironmentId,
            Key = req.Key,
            IsEnabled = false
        };
        
        _db.FeatureFlags.Add(newFlag);
        await _db.SaveChangesAsync(ct);
        
        await Send.CreatedAtAsync<GetFlagEndpoint>(
            routeValues: new { id = newFlag.Id },
            responseBody: new { newFlag.Id, newFlag.Key, newFlag.IsEnabled },
            cancellation: ct);
    }
}