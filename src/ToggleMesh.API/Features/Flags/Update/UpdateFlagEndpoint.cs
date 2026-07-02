using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Flags.Update;

public class UpdateFlagEndpoint : ToggleEndpoint<UpdateFlagRequest, GetFlagResponse>
{
    private readonly AppDbContext _db;
    
    public UpdateFlagEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/projects/{projectId}/environments/{environmentId}/flags/{key}");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(UpdateFlagRequest req, CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("key")!;

        var state = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.EnvironmentId == environmentId && x.FeatureFlag.Key == flagKey, ct);

        if (state is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        state.IsEnabled = req.IsEnabled;
        state.RolloutPercentage = req.RolloutPercentage;

        var existingRules = state.Rules.ToList();
        foreach (var oldRule in existingRules)
            if (!req.Rules.Any(r => 
                    r.GroupId == oldRule.GroupId 
                    && r.Attribute == oldRule.Attribute 
                    && r.Operator == oldRule.Operator 
                    && r.Value == oldRule.Value))
                _db.Remove(oldRule);
        
        foreach (var newRule in req.Rules)
            if (!existingRules.Any(r => 
                    r.GroupId == newRule.GroupId 
                    && r.Attribute == newRule.Attribute 
                    && r.Operator == newRule.Operator 
                    && r.Value == newRule.Value))
                state.Rules.Add(new FlagRule { 
                    GroupId = newRule.GroupId, 
                    Attribute = newRule.Attribute, 
                    Operator = newRule.Operator, 
                    Value = newRule.Value 
                });

        await _db.SaveChangesAsync(ct);

        var response = state.ToDto();
        await new NotifyFlagUpdatedCommand(
            environmentId, 
            flagKey, 
            response
        ).ExecuteAsync(ct);

        await Send.OkAsync(response, ct);
    }
}
