using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors.Audit.Analyzers;

// ReSharper disable once UnusedType.Global
public class VariationWeightAnalyzer : AuditAnalyzer<VariationWeight>
{
    protected override async Task<AuditMetadata> AnalyzeAsync(VariationWeight target, DbContext context, CancellationToken ct)
    {
        var stateEntry = context.ChangeTracker.Entries<FlagEnvironmentState>()
            .FirstOrDefault(e => e.Entity.FallthroughRollout?.Contains(target) == true);

        if (stateEntry == null) 
            return new AuditMetadata(
                "Unknown Rollout", 
                null, 
                null);
        
        var state = stateEntry.Entity;
        var flagKey = state.FeatureFlag?.Key;
        var projectId = state.FeatureFlag?.ProjectId;
        var envId = state.EnvironmentId;

        if (flagKey != null && projectId != null)
            return new AuditMetadata(
                FriendlyName: $"{flagKey} (Default Rollout)",
                ProjectId: projectId,
                EnvironmentId: envId);
            
        var dbData = await context.Set<FlagEnvironmentState>()
            .Where(s => s.Id == state.Id)
            .Select(s => new {
                FlagKey = s.FeatureFlag.Key,
                s.FeatureFlag.ProjectId
            })
            .FirstOrDefaultAsync(ct);

        if (dbData != null)
        {
            flagKey = dbData.FlagKey;
            projectId = dbData.ProjectId;
        }

        return new AuditMetadata(
            FriendlyName: flagKey != null ? $"{flagKey} (Default Rollout)" : "Unknown Rollout",
            ProjectId: projectId,
            EnvironmentId: envId);
    }
}
