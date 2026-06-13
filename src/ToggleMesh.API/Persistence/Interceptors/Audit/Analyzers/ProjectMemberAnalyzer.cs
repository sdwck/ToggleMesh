using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Persistence.Interceptors.Audit.Analyzers;

// ReSharper disable once UnusedType.Global
public class ProjectMemberAnalyzer : AuditAnalyzer<ProjectMember>
{
    protected override async Task<AuditMetadata> AnalyzeAsync(ProjectMember entity, DbContext context, CancellationToken ct)
    {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        var email = entity.User?.Email;
        if (string.IsNullOrEmpty(email))
            email = context.ChangeTracker.Entries<ApplicationUser>()
                .FirstOrDefault(x => x.Entity.Id == entity.UserId)?.Entity.Email;

        if (string.IsNullOrEmpty(email))
            email = await context.Set<ApplicationUser>()
                .Where(x => x.Id == entity.UserId)
                .Select(x => x.Email)
                .FirstOrDefaultAsync(ct);

        return new AuditMetadata(
            email ?? entity.UserId.ToString(),
            entity.ProjectId,
            null);
    }
}