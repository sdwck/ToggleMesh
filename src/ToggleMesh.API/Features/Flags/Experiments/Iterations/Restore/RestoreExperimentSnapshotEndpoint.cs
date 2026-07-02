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
            
        if (root.TryGetProperty("RolloutPercentage", out var rolloutProp) && rolloutProp.ValueKind != JsonValueKind.Null)
            state.RolloutPercentage = rolloutProp.GetInt32();
        else
            state.RolloutPercentage = null;

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

        if (state.ContextualRollouts != null && state.ContextualRollouts.Count != 0)
        {
            _db.ContextualRollouts.RemoveRange(state.ContextualRollouts);
            state.ContextualRollouts.Clear();
        }

        if (root.TryGetProperty("ContextualRollouts", out var contextualRolloutsProp))
        {
            state.ContextualRollouts ??= new List<ContextualRollout>();

            if (contextualRolloutsProp.ValueKind == JsonValueKind.Object)
                foreach (var prop in contextualRolloutsProp.EnumerateObject())
                    state.ContextualRollouts.Add(new ContextualRollout
                    {
                        ContextSlice = prop.Name,
                        RolloutPercentage = prop.Value.GetInt32()
                    });
            else if (contextualRolloutsProp.ValueKind == JsonValueKind.Array)
                foreach (var prop in contextualRolloutsProp.EnumerateArray())
                    if (prop.TryGetProperty("ContextSlice", out var sliceProp) && 
                        prop.TryGetProperty("RolloutPercentage", out var pctProp))
                        state.ContextualRollouts.Add(new ContextualRollout
                        {
                            ContextSlice = sliceProp.GetString() ?? "",
                            RolloutPercentage = pctProp.GetInt32()
                        });
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
                    GroupId = ruleElement.GetProperty("GroupId").GetInt32(),
                    Attribute = ruleElement.GetProperty("Attribute").GetString() ?? "",
                    Operator = ruleElement.GetProperty("Operator").GetString() ?? "",
                    Value = ruleElement.GetProperty("Value").GetString() ?? ""
                };

                state.Rules.Add(newRule);
            }

        state.IsExperimentActive = false;
        
        await _db.SaveChangesAsync(ct);

        var response = state.ToDto();
        await new NotifyFlagUpdatedCommand(environmentId, flagKey, response)
            .ExecuteAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
