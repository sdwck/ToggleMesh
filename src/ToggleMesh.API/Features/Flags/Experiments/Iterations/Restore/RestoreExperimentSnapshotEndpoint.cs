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

namespace ToggleMesh.API.Features.Flags.Experiments.Iterations.Restore;

public class RestoreExperimentSnapshotEndpoint : ToggleEndpointWithoutRequest<GetFlagResponse>
{
    private readonly AppDbContext _db;

    public RestoreExperimentSnapshotEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/environments/{environmentId}/flags/{key}/experiments/iterations/{iterationId:guid}/restore");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsEdit);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("key")!;
        var iterationId = Route<Guid>("iterationId");

        var iteration = await _db.ExperimentIterations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == iterationId && x.EnvironmentId == environmentId && x.FlagKey == flagKey, ct);

        if (iteration == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var state = await _db.FlagEnvironmentStates
            .Include(x => x.FeatureFlag)
                .ThenInclude(x => x.Variations)
            .Include(x => x.Rules)
            .Include(x => x.ContextualRollouts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.EnvironmentId == environmentId && x.FeatureFlag.Key == flagKey, ct);

        if (state == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (state.IsExperimentActive)
        {
            ThrowError("Cannot restore snapshot while an experiment is currently active. Please stop and save the current experiment first.");
            return;
        }

        var snapshotDoc = JsonDocument.Parse(iteration.FlagConfigSnapshot);
        var root = snapshotDoc.RootElement;

        if (root.TryGetProperty("IsEnabled", out var isEnabledProp))
            state.IsEnabled = isEnabledProp.GetBoolean();
            
        if (root.TryGetProperty("IsMabEnabled", out var isMabEnabledProp))
            state.IsMabEnabled = isMabEnabledProp.GetBoolean();

        if (root.TryGetProperty("ContextPartitionKeys", out var contextPartitionKeysProp) && contextPartitionKeysProp.ValueKind == JsonValueKind.Array)
        {
            state.ContextPartitionKeys = contextPartitionKeysProp.EnumerateArray()
                .Select(x => x.GetString()!)
                .ToArray();
        }
        else
            state.ContextPartitionKeys = [];

        if (root.TryGetProperty("FallthroughRollout", out var fallthroughProp) && fallthroughProp.ValueKind == JsonValueKind.Array)
        {
            state.FallthroughRollout.Clear();
            foreach (var r in fallthroughProp.EnumerateArray())
                state.FallthroughRollout.Add(new VariationWeight { 
                    VariationId = r.GetProperty("VariationId").GetGuid(), 
                    Weight = r.GetProperty("Weight").GetInt32() 
                });
        }

        if (state.ContextualRollouts.Count > 0)
        {
            _db.ContextualRollouts.RemoveRange(state.ContextualRollouts);
            state.ContextualRollouts.Clear();
        }

        if (root.TryGetProperty("ContextualRollouts", out var contextualRolloutsProp) && 
            contextualRolloutsProp.ValueKind == JsonValueKind.Array)
            foreach (var prop in contextualRolloutsProp.EnumerateArray())
            {
                if (!prop.TryGetProperty("ContextSlice", out var sliceProp) 
                    || !prop.TryGetProperty("Rollout", out var rolloutProp) 
                    || rolloutProp.ValueKind != JsonValueKind.Array) 
                    continue;
                    
                var cr = new ContextualRollout
                {
                    ContextSlice = sliceProp.GetString() ?? "",
                    IsAutoManaged = prop.TryGetProperty("IsAutoManaged", out var mProp) && mProp.GetBoolean()
                };
                        
                foreach (var r in rolloutProp.EnumerateArray())
                    cr.Rollout.Add(new VariationWeight { 
                        VariationId = r.GetProperty("VariationId").GetGuid(), 
                        Weight = r.GetProperty("Weight").GetInt32() 
                    });
                        
                state.ContextualRollouts.Add(cr);
            }

        if (state.Rules.Count != 0)
        {
            _db.FlagRules.RemoveRange(state.Rules);
            state.Rules.Clear();
        }

        if (root.TryGetProperty("Rules", out var rulesProp) && rulesProp.ValueKind == JsonValueKind.Array)
            foreach (var ruleElement in rulesProp.EnumerateArray())
            {
                var newRule = new FlagRule
                {
                    Id = Guid.Empty,
                    FlagEnvironmentStateId = state.Id,
                    Priority = ruleElement.TryGetProperty("Priority", out var pProp) ? pProp.GetInt32() : 0,
                    GroupId = ruleElement.GetProperty("GroupId").GetInt32(),
                    Attribute = ruleElement.GetProperty("Attribute").GetString() ?? "",
                    Operator = ruleElement.GetProperty("Operator").GetString() ?? "",
                    Value = ruleElement.GetProperty("Value").GetString() ?? ""
                };
                
                if (ruleElement.TryGetProperty("Rollout", out var rProp) && rProp.ValueKind == JsonValueKind.Array)
                    foreach (var r in rProp.EnumerateArray())
                        newRule.Rollout.Add(new VariationWeight { 
                            VariationId = r.GetProperty("VariationId").GetGuid(), 
                            Weight = r.GetProperty("Weight").GetInt32() 
                        });

                state.Rules.Add(newRule);
            }

        state.IsExperimentActive = false;
        await _db.SaveChangesAsync(ct);

        var response = state.ToDto();
        await new NotifyFlagUpdatedCommand(environmentId, flagKey, response, state.ToSdkDto())
            .ExecuteAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
