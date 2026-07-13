using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors.Audit.Analyzers;

// ReSharper disable once UnusedType.Global
public class ContextualRolloutAnalyzer : AuditAnalyzer<ContextualRollout>
{
    protected override async Task<AuditMetadata> AnalyzeAsync(ContextualRollout rollout, DbContext context, CancellationToken ct)
    {
        var state = context.ChangeTracker.Entries<FlagEnvironmentState>()
            .FirstOrDefault(e => e.Entity.Id == rollout.FlagEnvironmentStateId)?.Entity;

        var flagKey = state?.FeatureFlag?.Key;
        var projectId = state?.FeatureFlag?.ProjectId;
        var envId = state?.EnvironmentId;
        
        if (flagKey == null || projectId == null || envId == null)
        {
            var dbData = await context.Set<FlagEnvironmentState>()
                .Where(s => s.Id == rollout.FlagEnvironmentStateId)
                .Select(s => new {
                    EnvId = s.EnvironmentId,
                    FlagKey = s.FeatureFlag.Key,
                    s.FeatureFlag.ProjectId
                })
                .FirstOrDefaultAsync(ct);

            if (dbData != null)
            {
                flagKey = dbData.FlagKey;
                projectId = dbData.ProjectId;
                envId = dbData.EnvId;
            }
        }

        return new AuditMetadata(
            FriendlyName: flagKey != null 
                ? $"{flagKey} (Rollout Context)" 
                : "Unknown Rollout Context",
            ProjectId: projectId,
            EnvironmentId: envId
        );
    }
}
