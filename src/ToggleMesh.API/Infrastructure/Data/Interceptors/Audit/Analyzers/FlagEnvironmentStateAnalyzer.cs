using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors.Audit.Analyzers;

// ReSharper disable once UnusedType.Global
public class FlagEnvironmentStateAnalyzer : AuditAnalyzer<FlagEnvironmentState>
{
    protected override async Task<AuditMetadata> AnalyzeAsync(FlagEnvironmentState state, DbContext context, CancellationToken ct)
    {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        var flagKey = state.FeatureFlag?.Key;
        var projectId = state.FeatureFlag?.ProjectId;

        if (flagKey == null || projectId == null)
        {
            var dbData = await context.Set<FeatureFlag>()
                .Where(f => f.Id == state.FeatureFlagId)
                .Select(f => new { f.Key, f.ProjectId })
                .FirstOrDefaultAsync(ct);

            if (dbData != null)
            {
                flagKey = dbData.Key;
                projectId = dbData.ProjectId;
            }
        }

        return new AuditMetadata(
            FriendlyName: flagKey ?? state.Id.ToString(),
            ProjectId: projectId,
            EnvironmentId: state.EnvironmentId
        );
    }
}