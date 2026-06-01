using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Get;
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
            IsEnabled = false,
            RolloutPercentage = req.RolloutPercentage,
            Rules = req.Rules.Select(r => new FlagRule
            {
                Attribute = r.Attribute,
                Operator = r.Operator,
                Value = r.Value
            }).ToList()
        };
        
        _db.FeatureFlags.Add(newFlag);
        await _db.SaveChangesAsync(ct);
        
        var response = new GetFlagResponse(
            newFlag.Key, 
            newFlag.IsEnabled, 
            newFlag.Rules.Select(r => new RuleDto(r.Attribute, r.Operator, r.Value)),
            newFlag.RolloutPercentage);

        await Send.CreatedAtAsync<GetFlagEndpoint>(
            routeValues: new { id = newFlag.Id },
            responseBody: response,
            cancellation: ct);
    }
}