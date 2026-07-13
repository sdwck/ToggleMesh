using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors.Audit.Analyzers;

// ReSharper disable once UnusedType.Global
public class FlagIndividualTargetAnalyzer : AuditAnalyzer<FlagIndividualTarget>
{
    protected override async Task<AuditMetadata> AnalyzeAsync(FlagIndividualTarget target, DbContext context, CancellationToken ct)
    {
        var state = context.ChangeTracker.Entries<FlagEnvironmentState>()
            .FirstOrDefault(e => e.Entity.Id == target.FlagEnvironmentStateId)?.Entity;

        var flagKey = state?.FeatureFlag?.Key;
        var projectId = state?.FeatureFlag?.ProjectId;
        var envId = state?.EnvironmentId;

        if (flagKey != null && projectId != null && envId != null)
            return new AuditMetadata(
                FriendlyName: $"{flagKey} (Individual Target)",
                ProjectId: projectId,
                EnvironmentId: envId);
        
        var dbData = await context.Set<FlagEnvironmentState>()
            .Where(s => s.Id == target.FlagEnvironmentStateId)
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

        return new AuditMetadata(
            FriendlyName: flagKey != null 
                ? $"{flagKey} (Individual Target)" 
                : "Unknown Individual Target",
            ProjectId: projectId,
            EnvironmentId: envId
        );
    }
}
