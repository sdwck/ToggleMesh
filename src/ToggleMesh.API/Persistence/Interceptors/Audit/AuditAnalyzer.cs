using Microsoft.EntityFrameworkCore;

namespace ToggleMesh.API.Persistence.Interceptors.Audit;

public abstract class AuditAnalyzer<T> : IAuditAnalyzer
    where T : class
{
    public Type EntityType => typeof(T);
    public Task<AuditMetadata> AnalyzeAsync(object entity, DbContext context, CancellationToken ct) =>
        AnalyzeAsync((T)entity, context, ct);

    protected abstract Task<AuditMetadata> AnalyzeAsync(T entity, DbContext context, CancellationToken ct);
}