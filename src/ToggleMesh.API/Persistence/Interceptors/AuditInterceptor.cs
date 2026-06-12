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
            if (entry.Entity is AuditLog || entry.Entity is ToggleMesh.API.Features.Projects.EnvironmentKey || entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            Guid? performedById = null;
            string performedByEmail = "System";

            if (_httpContextAccessor.HttpContext?.User is not null)
            {
                var user = _httpContextAccessor.HttpContext.User;
                if (user.TryGetUserId(out var userId))
                {
                    performedById = userId;
                }

                var email = user.FindFirst("email")?.Value 
                    ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                if (!string.IsNullOrEmpty(email))
                {
                    performedByEmail = email;
                }
                else if (performedById.HasValue)
                {
                    performedByEmail = "Unknown Email";
                }
            }

            var entityFriendlyName = string.Empty;
            var keyProp = entry.Metadata.FindProperty("Key");
            if (keyProp != null)
            {
                entityFriendlyName = entry.Property(keyProp.Name).CurrentValue?.ToString() ?? string.Empty;
            }
            else
            {
                var nameProp = entry.Metadata.FindProperty("Name");
                if (nameProp != null)
                {
                    entityFriendlyName = entry.Property(nameProp.Name).CurrentValue?.ToString() ?? string.Empty;
                }
            }

            var auditLog = new AuditLog
            {
                Id = Guid.CreateVersion7(),
                EntityName = entry.Entity.GetType().Name,
                EntityFriendlyName = entityFriendlyName,
                Action = entry.State.ToString(),
                Timestamp = DateTime.UtcNow,
                PerformedById = performedById,
                PerformedByEmail = performedByEmail
            };

            var projectIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "ProjectId");
            if (projectIdProp != null && projectIdProp.CurrentValue is Guid pid)
                auditLog.ProjectId = pid;

            if (entry.Entity is IHasEnvironment envEntity)
                auditLog.EnvironmentId = envEntity.EnvironmentId;
            else if (entry.Entity is FlagRule rule)
            {
                var parentEntry = context.ChangeTracker.Entries<FlagEnvironmentState>()
                    .FirstOrDefault(f =>
                        f.Entity == rule.FlagEnvironmentState ||
                        (rule.FlagEnvironmentStateId != Guid.Empty && f.Entity.Id == rule.FlagEnvironmentStateId));
                if (parentEntry != null)
                {
                    auditLog.EnvironmentId = parentEntry.Entity.EnvironmentId;
                    if (parentEntry.Entity.FeatureFlag != null)
                        auditLog.ProjectId = parentEntry.Entity.FeatureFlag.ProjectId;
                }
            }
            else if (entry.Entity is FlagEnvironmentState state)
            {
                if (state.FeatureFlag != null)
                    auditLog.ProjectId = state.FeatureFlag.ProjectId;
                else
                {
                    var flagEntry = context.ChangeTracker.Entries<FeatureFlag>().FirstOrDefault(f => f.Entity.Id == state.FeatureFlagId);
                    if (flagEntry != null)
                        auditLog.ProjectId = flagEntry.Entity.ProjectId;
                }
            }
            else if (entry.Entity is ToggleMesh.API.Features.Projects.Project)
            {
                var idProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
                if (idProp != null && idProp.CurrentValue is Guid pidVal)
                    auditLog.ProjectId = pidVal;
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