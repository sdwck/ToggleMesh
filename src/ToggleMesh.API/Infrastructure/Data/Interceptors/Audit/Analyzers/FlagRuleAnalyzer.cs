using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors.Audit.Analyzers;

// ReSharper disable once UnusedType.Global
public class FlagRuleAnalyzer : AuditAnalyzer<FlagRule>
{
    protected override async Task<AuditMetadata> AnalyzeAsync(FlagRule rule, DbContext context, CancellationToken ct)
    {
        var state = context.ChangeTracker.Entries<FlagEnvironmentState>()
            .FirstOrDefault(e => e.Entity.Id == rule.FlagEnvironmentStateId)?.Entity;

        var flagKey = state?.FeatureFlag?.Key;
        var projectId = state?.FeatureFlag?.ProjectId;
        var envId = state?.EnvironmentId;
        
        if (flagKey == null || projectId == null || envId == null)
        {
            var dbData = await context.Set<FlagEnvironmentState>()
                .Where(s => s.Id == rule.FlagEnvironmentStateId)
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
                ? $"{flagKey} (Rule)" 
                : "Unknown Rule",
            ProjectId: projectId,
            EnvironmentId: envId
        );
    }
}