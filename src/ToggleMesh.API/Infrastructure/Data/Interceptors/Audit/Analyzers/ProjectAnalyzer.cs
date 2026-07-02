using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors.Audit.Analyzers;

public class ProjectAnalyzer : AuditAnalyzer<Project>
{
    protected override Task<AuditMetadata> AnalyzeAsync(
        Project entity, 
        DbContext context, 
        CancellationToken ct)
    {
        return Task.FromResult(new AuditMetadata(
            FriendlyName: entity.Name,
            ProjectId: entity.Id,
            EnvironmentId: null
        ));
    }
}
