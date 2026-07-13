using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors.Audit.Analyzers;

// ReSharper disable once UnusedType.Global
public class FlagVariationAnalyzer : AuditAnalyzer<FlagVariation>
{
    protected override async Task<AuditMetadata> AnalyzeAsync(FlagVariation variation, DbContext context, CancellationToken ct)
    {
        var flag = context.ChangeTracker.Entries<FeatureFlag>()
            .FirstOrDefault(e => e.Entity.Id == variation.FeatureFlagId)?.Entity;

        var flagKey = flag?.Key;
        var projectId = flag?.ProjectId;
        
        if (flagKey == null || projectId == null)
        {
            var dbData = await context.Set<FeatureFlag>()
                .Where(s => s.Id == variation.FeatureFlagId)
                .Select(s => new {
                    s.Key,
                    s.ProjectId
                })
                .FirstOrDefaultAsync(ct);

            if (dbData != null)
            {
                flagKey = dbData.Key;
                projectId = dbData.ProjectId;
            }
        }

        return new AuditMetadata(
            FriendlyName: flagKey != null 
                ? $"{flagKey} (Variation)" 
                : "Unknown Variation",
            ProjectId: projectId,
            EnvironmentId: null
        );
    }
}
