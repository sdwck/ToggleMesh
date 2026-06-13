using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Audit;
using ToggleMesh.API.Persistence.Interceptors.Audit;
using ToggleMesh.API.Persistence.Interceptors.Audit.Analyzers;

namespace ToggleMesh.API.Persistence.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Dictionary<Type, IAuditAnalyzer> _analyzers;
    private readonly DefaultAnalyzer _defaultAnalyzer = new();

    public AuditInterceptor(
        IHttpContextAccessor httpContextAccessor, 
        IEnumerable<IAuditAnalyzer> analyzers)
    {
        _httpContextAccessor = httpContextAccessor;
        _analyzers = analyzers.ToDictionary(x => x.EntityType);
    }
    
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (_httpContextAccessor.HttpContext != null)
            throw new InvalidOperationException(
                "Synchronous SaveChanges() is prohibited during web requests.");

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        await CreateAuditLogs(eventData.Context, ct);
        return await base.SavingChangesAsync(eventData, result, ct);
    }

    private async Task CreateAuditLogs(
        DbContext? context,
        CancellationToken ct = default)
    {
        if (context is null) 
            return;
        
        var user = _httpContextAccessor.HttpContext?.User;

        Guid? actorId = null;
        if (user != null && user.TryGetUserId(out var parsedUserId))
            actorId = parsedUserId;

        var actorEmail = user?.FindFirst("email")?.Value
                         ?? user?.FindFirst(ClaimTypes.Email)?.Value
                         ?? string.Empty;

        var auditEntries = new List<AuditLog>();
        var modifiedEntries = context.ChangeTracker.Entries()
            .Where(e => 
                e.Entity is not AuditLog 
                && e.State is not 
                    (EntityState.Detached or EntityState.Unchanged))
            .ToList();
        
        foreach (var entry in modifiedEntries)
        {
            var entityType = entry.Metadata.ClrType;
            if (!_analyzers.TryGetValue(entityType, out var analyzer))
                analyzer = _defaultAnalyzer;

            var metadata = await analyzer.AnalyzeAsync(entry.Entity, context, ct);
            var (oldVals, newVals) = GetSnapshot(entry);
            if (oldVals == null && newVals == null)
                continue;
            
            auditEntries.Add(new AuditLog
            {
                Id = Guid.CreateVersion7(),
                EntityName = entityType.Name,
                EntityFriendlyName = metadata.FriendlyName,
                EntityId = entry.Property("Id").CurrentValue?.ToString() ?? "New",
                Action = entry.State.ToString(),
                Timestamp = DateTime.UtcNow,
                ProjectId = metadata.ProjectId,
                EnvironmentId = metadata.EnvironmentId,
                PerformedById = actorId,
                PerformedByEmail = actorEmail,
                OldValues = oldVals != null ? JsonSerializer.Serialize(oldVals) : "{}",
                NewValues = newVals != null ? JsonSerializer.Serialize(newVals) : "{}"
            });
        }

        if (auditEntries.Count > 0)
            context.Set<AuditLog>().AddRange(auditEntries);
    }
    
    private (Dictionary<string, object?>? oldVals, Dictionary<string, object?>? newVals) GetSnapshot(EntityEntry entry)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (name == "Id" || name.EndsWith("Id") || name == "KeyHash") 
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[name] = Format(property.CurrentValue);
                    break;
                case EntityState.Deleted:
                    oldValues[name] = Format(property.OriginalValue);
                    break;
                case EntityState.Modified:
                    if (property.IsModified)
                    {
                        oldValues[name] = Format(property.OriginalValue);
                        newValues[name] = Format(property.CurrentValue);
                    }
                    break;
            }
        }

        return (oldValues.Count > 0 
                ? oldValues 
                : null, newValues.Count > 0 ? newValues : null);

        object? Format(object? val) => 
            val?.GetType().IsEnum == true
                ? val.ToString() 
                : val;
    }
}