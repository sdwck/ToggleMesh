using Microsoft.EntityFrameworkCore;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors.Audit.Analyzers;

public class DefaultAnalyzer : IAuditAnalyzer
{
    public Type EntityType => typeof(object);

    public Task<AuditMetadata> AnalyzeAsync(object entity, DbContext context, CancellationToken ct)
    {
        var entry = context.Entry(entity);
        
        var name = entry.Metadata.FindProperty("Key")?.PropertyInfo?.GetValue(entity)?.ToString()
                   ?? entry.Metadata.FindProperty("Name")?.PropertyInfo?.GetValue(entity)?.ToString()
                   ?? entry.Metadata.FindProperty("Email")?.PropertyInfo?.GetValue(entity)?.ToString()
                   ?? entity.GetType().Name;

        var pId = entry.Metadata.FindProperty("ProjectId")?.PropertyInfo?.GetValue(entity) as Guid?;
        var eId = entry.Metadata.FindProperty("EnvironmentId")?.PropertyInfo?.GetValue(entity) as Guid?;

        return Task.FromResult(new AuditMetadata(name, pId, eId));
    }
}