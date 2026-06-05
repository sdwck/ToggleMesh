using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Audit;
using ToggleMesh.API.Features.Flags;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Persistence.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CreateAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        CreateAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void CreateAuditLogs(DbContext? context)
    {
        if (context is null) 
            return;

        var auditEntries = new List<AuditLog>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            var performedBy = string.Empty;
            if (_httpContextAccessor.HttpContext?.User is not null && 
                _httpContextAccessor.HttpContext.User.TryGetUserId(out var userId))
                performedBy = userId.ToString();

            var auditLog = new AuditLog
            {
                Id = Guid.CreateVersion7(),
                EntityName = entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                Timestamp = DateTime.UtcNow,
                PerformedBy = performedBy
            };

            if (entry.Entity is IHasEnvironment envEntity)
                auditLog.EnvironmentId = envEntity.EnvironmentId;
            else if (entry.Entity is FlagRule rule)
            {
                var parentEntry = context.ChangeTracker.Entries<FeatureFlag>()
                    .FirstOrDefault(f =>
                        f.Entity == rule.FeatureFlag ||
                        (rule.FeatureFlagId != 0 && f.Entity.Id == rule.FeatureFlagId));
                if (parentEntry != null)
                    auditLog.EnvironmentId = parentEntry.Entity.EnvironmentId;
            }

            var primaryKey = entry.Metadata.FindPrimaryKey();
            if (primaryKey != null)
            {
                var pkProperty = entry.Property(primaryKey.Properties[0].Name);
                auditLog.EntityId = pkProperty.IsTemporary
                    ? "New"
                    : pkProperty.CurrentValue?.ToString() ?? "Unknown";
            }

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();

            foreach (var property in entry.Properties)
            {
                var propertyName = property.Metadata.Name;

                if (propertyName == "Id" || propertyName.EndsWith("Id")) continue;

                switch (entry.State)
                {
                    case EntityState.Added:
                        newValues[propertyName] = property.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        oldValues[propertyName] = property.OriginalValue;
                        break;
                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            oldValues[propertyName] = property.OriginalValue;
                            newValues[propertyName] = property.CurrentValue;
                        }

                        break;
                }
            }

            if (oldValues.Count == 0 && newValues.Count == 0) continue;

            auditLog.OldValues = JsonSerializer.Serialize(oldValues);
            auditLog.NewValues = JsonSerializer.Serialize(newValues);
            auditEntries.Add(auditLog);
        }

        if (auditEntries.Count != 0)
            context.Set<AuditLog>().AddRange(auditEntries);
    }
}