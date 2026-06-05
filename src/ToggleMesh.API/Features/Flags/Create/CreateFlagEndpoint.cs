using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Create;

public class CreateFlagEndpoint : ToggleEndpoint<CreateFlagRequest>
{
    private readonly AppDbContext _db;

    public CreateFlagEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/environments/{environmentId}/flags");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.FlagsCreate}");
    }

    public override async Task HandleAsync(CreateFlagRequest req, CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        
        var exists = await _db.FeatureFlags
            .AnyAsync(x => x.EnvironmentId == environmentId && x.Key == req.Key, ct);
        if (exists)
        {
            AddError(x => x.Key, 
                "A feature flag with this key already exists.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var newFlag = new FeatureFlag
        {
            EnvironmentId = environmentId,
            Key = req.Key,
            IsEnabled = false,
            RolloutPercentage = req.RolloutPercentage,
            Rules = req.Rules.Select(r => new FlagRule
            {
                GroupId = r.GroupId,
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
            newFlag.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)),
            newFlag.RolloutPercentage);

        await Send.CreatedAtAsync<GetFlagEndpoint>(
            routeValues: new { id = newFlag.Id },
            responseBody: response,
            cancellation: ct);
    }
}