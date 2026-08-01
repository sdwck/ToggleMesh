using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Update;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Flags.ScheduledChanges;

public static class FlagPatchApplicationHelper
{
    public static async Task<FlagEnvironmentState> ApplyPatchAsync(AppDbContext db, PendingFlagChange change, CancellationToken ct)
    {
        var state = await db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
                .ThenInclude(x => x.Variations)
            .Include(x => x.Rules)
            .Include(x => x.IndividualTargets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.EnvironmentId == change.EnvironmentId && x.FeatureFlagId == change.FlagId, ct);

        if (state == null)
            throw new Exception("Flag environment state not found.");

        var req = JsonSerializer.Deserialize<UpdateFlagRequest>(change.PatchInstructionsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (req == null)
            throw new Exception("Failed to deserialize PatchInstructionsJson.");

        if (req.OffVariationId != null)
            state.OffVariationId = req.OffVariationId;

        if (req.IsEnabled.HasValue)
            state.IsEnabled = req.IsEnabled.Value;

        if (state.IsExperimentActive)
            throw new Exception("Cannot apply patch because an experiment is currently active.");
            if (req.FallthroughRollout != null)
                state.FallthroughRollout = req.FallthroughRollout;

            if (req.Rules != null)
            {
                db.FlagRules.RemoveRange(state.Rules);
                state.Rules.Clear();
                
                foreach (var newRule in req.Rules)
                    state.Rules.Add(new FlagRule 
                    { 
                        GroupId = newRule.GroupId, 
                        Attribute = newRule.Attribute, 
                        Operator = newRule.Operator, 
                        Value = newRule.Value,
                        Rollout = newRule.Rollout?
                            .Select(r => new VariationWeight { VariationId = r.VariationId, Weight = r.Weight })
                            .ToList() ?? []
                    });
            }

        if (req.IndividualTargets != null)
        {
            db.RemoveRange(state.IndividualTargets);
            state.IndividualTargets.Clear();

            foreach (var kvp in req.IndividualTargets)
                state.IndividualTargets.Add(new FlagIndividualTarget
                {
                    IdentityKey = kvp.Key,
                    VariationId = kvp.Value
                });
        }

        change.Status = PendingFlagChangeStatus.Executed;
        await db.SaveChangesAsync(ct);
        return state;
    }
}
