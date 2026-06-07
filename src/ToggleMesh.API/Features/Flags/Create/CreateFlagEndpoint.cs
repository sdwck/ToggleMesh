using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.Features.Flags.Create;

public class CreateFlagEndpoint : ToggleEndpoint<CreateFlagRequest, GetFlagResponse>
{
    private readonly AppDbContext _db;

    public CreateFlagEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/flags");
        Version(1);
        Policies($"Permission:{Auth.Models.Permissions.FlagsCreate}");
    }

    public override async Task HandleAsync(CreateFlagRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        
        var exists = await _db.FeatureFlags
            .AnyAsync(x => x.ProjectId == projectId && x.Key == req.Key, ct);
        
        if (exists)
        {
            AddError(x => x.Key, 
                "A feature flag with this key already exists in this project.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var newFlag = new FeatureFlag
        {
            ProjectId = projectId,
            Key = req.Key,
            CreatedAt = DateTime.UtcNow
        };
        
        _db.FeatureFlags.Add(newFlag);
        
        var environments = await _db.Environments
            .Where(e => e.ProjectId == projectId)
            .Select(e => e.Id)
            .ToListAsync(ct);
        
        var safeRules = req.Rules;
        
        foreach(var envId in environments)
        {
            var state = new FlagEnvironmentState
            {
                FeatureFlag = newFlag,
                EnvironmentId = envId,
                IsEnabled = false,
                RolloutPercentage = req.RolloutPercentage,
                Rules = safeRules.Select(r => new FlagRule
                {
                    GroupId = r.GroupId,
                    Attribute = r.Attribute,
                    Operator = r.Operator,
                    Value = r.Value
                }).ToList()
            };
            _db.FlagEnvironmentStates.Add(state);
        }
        
        await _db.SaveChangesAsync(ct);
        
        var response = new GetFlagResponse(
            newFlag.Key, 
            false, 
            safeRules,
            req.RolloutPercentage);

        await Send.CreatedAtAsync<GetAll.GetFlagsEndpoint>(
            routeValues: new { projectId },
            responseBody: response,
            cancellation: ct);
    }
}
