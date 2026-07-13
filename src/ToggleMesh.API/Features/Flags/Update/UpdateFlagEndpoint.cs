using System.Text.Json;
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
                .ThenInclude(x => x.Variations)
            .Include(x => x.Rules)
            .Include(x => x.IndividualTargets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.EnvironmentId == environmentId && x.FeatureFlag.Key == flagKey, ct);

        if (state is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        
        state.OffVariationId = req.OffVariationId;

        if (!state.IsExperimentActive)
        {
            var currentRolloutJson = JsonSerializer.Serialize(state.FallthroughRollout);
            var newRolloutJson = JsonSerializer.Serialize(req.FallthroughRollout);
            if (currentRolloutJson != newRolloutJson)
                state.FallthroughRollout = req.FallthroughRollout;

            var currentRulesJson = JsonSerializer.Serialize(
                state.Rules.Select(r => 
                    new { 
                        r.GroupId, 
                        r.Attribute, 
                        r.Operator, 
                        r.Value, 
                        r.Rollout 
                    }));
            var newRulesJson = JsonSerializer.Serialize(
                req.Rules.Select(r => 
                    new
                    {
                        r.GroupId, 
                        r.Attribute, 
                        r.Operator, 
                        r.Value, 
                        Rollout = r.Rollout?.Select(rw => 
                            new { rw.VariationId, rw.Weight })
                    }));
            
            if (currentRulesJson != newRulesJson)
            {
                _db.FlagRules.RemoveRange(state.Rules);
                state.Rules.Clear();
                
                foreach (var newRule in req.Rules)
                    state.Rules.Add(new FlagRule 
                    { 
                        GroupId = newRule.GroupId, 
                        Attribute = newRule.Attribute, 
                        Operator = newRule.Operator, 
                        Value = newRule.Value,
                        Rollout = newRule.Rollout?
                            .Select(r => 
                                new VariationWeight
                                {
                                    VariationId = r.VariationId, 
                                    Weight = r.Weight
                                })
                            .ToList() ?? []
                    });
            }
        }

        var currentTargetsJson = JsonSerializer.Serialize(
            state.IndividualTargets
                .OrderBy(t => t.IdentityKey)
                .Select(t => new { t.IdentityKey, t.VariationId }));
        var newTargets = req.IndividualTargets?
            .OrderBy(t => t.Key)
            .Select(t => 
                new { IdentityKey = t.Key, VariationId = t.Value })
            .ToList() ?? [];
        var newTargetsJson = JsonSerializer.Serialize(newTargets);

        if (currentTargetsJson != newTargetsJson)
        {
            _db.RemoveRange(state.IndividualTargets);
            state.IndividualTargets.Clear();

            if (req.IndividualTargets != null)
                foreach (var kvp in req.IndividualTargets)
                    state.IndividualTargets.Add(new FlagIndividualTarget
                    {
                        IdentityKey = kvp.Key,
                        VariationId = kvp.Value
                    });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ThrowError("Concurrent update detected. The flag was modified by another process. Please refresh and try again.", 409);
        }

        var response = state.ToDto();
        await new NotifyFlagUpdatedCommand(
            environmentId, 
            flagKey, 
            response,
            state.ToSdkDto()
        ).ExecuteAsync(ct);

        await Send.OkAsync(response, ct);
    }
}
