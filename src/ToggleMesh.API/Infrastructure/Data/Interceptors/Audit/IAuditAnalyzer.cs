using Microsoft.EntityFrameworkCore;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors.Audit;

public interface IAuditAnalyzer
{
    Type EntityType { get; }
    Task<AuditMetadata> AnalyzeAsync(object entity, DbContext context, CancellationToken ct);
}