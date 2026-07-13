using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Infrastructure.Caching;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using StackExchange.Redis;
using ToggleMesh.API.Features.Flags.Commands;

namespace ToggleMesh.API.Features.Flags.UpdateGlobalSettings;

public class UpdateGlobalFlagSettingsEndpoint : ToggleEndpoint<UpdateGlobalFlagSettingsRequest>
{
    private readonly AppDbContext _db;

    public UpdateGlobalFlagSettingsEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Put("/projects/{projectId}/flags/{key}/settings");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(UpdateGlobalFlagSettingsRequest req, CancellationToken ct)
    {
        var projectId = Route<Guid>("projectId");
        var flagKey = Route<string>("key")!;

        var flag = await _db.FeatureFlags
            .Include(x => x.Variations)
            .FirstOrDefaultAsync(x => 
                x.ProjectId == projectId && x.Key == flagKey, ct);

        if (flag is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        flag.Name = req.Name;
        flag.Description = req.Description;
        flag.Tags = req.Tags;

        var existingVariations = flag.Variations.ToList();
        var deletedVariationIds = existingVariations
            .Select(v => v.Id)
            .Except(req.Variations.Select(v => v.Id))
            .ToList();

        if (deletedVariationIds.Count > 0)
        {
            var flagStates = await _db.FlagEnvironmentStates
                .Include(x => x.Environment)
                .Include(x => x.Rules)
                .Include(x => x.ContextualRollouts)
                .Where(x => x.FeatureFlagId == flag.Id)
                .ToListAsync(ct);

            foreach (var state in flagStates)
            {
                if (state.OffVariationId.HasValue && deletedVariationIds.Contains(state.OffVariationId.Value))
                {
                    var deletedVarValue = existingVariations.First(v => v.Id == state.OffVariationId.Value).Value;
                    ThrowError($"Cannot delete variation '{deletedVarValue}' because it is configured as the Off variation in environment '{state.Environment.Name}'.", 400);
                }

                foreach (var w in state.FallthroughRollout)
                {
                    if (w.Weight > 0 && deletedVariationIds.Contains(w.VariationId))
                    {
                        var deletedVarValue = existingVariations.First(v => v.Id == w.VariationId).Value;
                        ThrowError($"Cannot delete variation '{deletedVarValue}' because it has non-zero rollout weight in the default rollout of environment '{state.Environment.Name}'.", 400);
                    }
                }

                foreach (var r in state.Rules)
                {
                    foreach (var w in r.Rollout)
                    {
                        if (w.Weight > 0 && deletedVariationIds.Contains(w.VariationId))
                        {
                            var deletedVarValue = existingVariations.First(v => v.Id == w.VariationId).Value;
                            ThrowError($"Cannot delete variation '{deletedVarValue}' because it is used in targeting rules in environment '{state.Environment.Name}'.", 400);
                        }
                    }
                }

                foreach (var cr in state.ContextualRollouts)
                {
                    foreach (var w in cr.Rollout)
                    {
                        if (w.Weight > 0 && deletedVariationIds.Contains(w.VariationId))
                        {
                            var deletedVarValue = existingVariations.First(v => v.Id == w.VariationId).Value;
                            ThrowError($"Cannot delete variation '{deletedVarValue}' because it is used in contextual rollouts in environment '{state.Environment.Name}'.", 400);
                        }
                    }
                }
            }
        }
            
        foreach (var oldVar in existingVariations)
            if (!req.Variations.Any(v => v.Id == oldVar.Id))
                _db.Remove(oldVar);
                
        for (var i = 0; i < req.Variations.Count; i++)
        {
            var newVar = req.Variations[i];
            var existing = existingVariations.FirstOrDefault(v => v.Id == newVar.Id);
            if (existing != null)
            {
                existing.Value = newVar.Value;
                existing.Sequence = i;
            }
            else
            {
                var newFlagVar = new FlagVariation { Id = newVar.Id, Key = newVar.Id.ToString(), Name = newVar.Id.ToString(), Value = newVar.Value, Sequence = i };
                flag.Variations.Add(newFlagVar);
                _db.Entry(newFlagVar).State = EntityState.Added;
            }
        }

        await _db.SaveChangesAsync(ct);

        var states = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
                .ThenInclude(x => x.Variations)
            .Include(x => x.Rules)
            .Include(x => x.ContextualRollouts)
            .Where(x => x.FeatureFlag.ProjectId == projectId && x.FeatureFlag.Key == flagKey)
            .AsSplitQuery()
            .ToListAsync(ct);

        foreach (var state in states)
            await new NotifyFlagUpdatedCommand(state.EnvironmentId, flagKey, state.ToDto(), state.ToSdkDto()).ExecuteAsync(ct);

        await Send.NoContentAsync(ct);
    }
}
