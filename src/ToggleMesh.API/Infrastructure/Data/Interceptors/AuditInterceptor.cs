using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Audit.Domain;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data.Abstractions;
using ToggleMesh.API.Infrastructure.Data.Interceptors.Audit;
using ToggleMesh.API.Infrastructure.Data.Interceptors.Audit.Analyzers;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Dictionary<Type, IAuditAnalyzer> _analyzers;
    private readonly DefaultAnalyzer _defaultAnalyzer = new();
    private readonly Channel<WebhookEvent> _webhookChannel;
    private readonly ConditionalWeakTable<DbContext, List<WebhookEvent>> _webhookEventsTable = new();
    private readonly TimeProvider _timeProvider;

    public AuditInterceptor(
        IHttpContextAccessor httpContextAccessor, 
        IEnumerable<IAuditAnalyzer> analyzers, 
        Channel<WebhookEvent> webhookChannel,
        TimeProvider timeProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _webhookChannel = webhookChannel;
        _timeProvider = timeProvider;
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

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, 
        int result,
        CancellationToken cancellationToken = new())
    {
        if (eventData.Context != null && _webhookEventsTable.TryGetValue(eventData.Context, out var events))
        {
            foreach (var ev in events)
                await _webhookChannel.Writer.WriteAsync(ev, cancellationToken);
            _webhookEventsTable.Remove(eventData.Context);
        }
        
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
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
                         ?? user?.Identity?.Name
                         ?? string.Empty;

        var auditEntries = new List<AuditLog>();
        if (!_webhookEventsTable.TryGetValue(context, out var webhookEvents))
        {
            webhookEvents = new List<WebhookEvent>();
        }
        
        var modifiedEntries = context.ChangeTracker.Entries()
            .Where(e => 
                e.Entity is not AuditLog 
                and not WebhookDelivery 
                and not RefreshToken
                and not ExperimentMetric
                and not ContextualExperimentMetric
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
            
            var action = entry.State.ToString();
            if (entry is { State: EntityState.Modified, Entity: ISoftDeletable })
            {
                var isDeletedProp = entry.Property("IsDeleted");
                if (isDeletedProp is { IsModified: true, CurrentValue: true })
                    action = "Deleted";
                else if (isDeletedProp is { IsModified: true, CurrentValue: false })
                    action = "Restored";
            }

            auditEntries.Add(new AuditLog
            {
                Id = Guid.CreateVersion7(),
                EntityName = entityType.Name,
                EntityFriendlyName = metadata.FriendlyName,
                EntityId = entry.Property("Id").CurrentValue?.ToString() ?? "New",
                Action = action,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                ProjectId = metadata.ProjectId,
                EnvironmentId = metadata.EnvironmentId,
                PerformedById = actorId,
                PerformedByEmail = actorEmail,
                OldValues = oldVals != null ? JsonSerializer.Serialize(oldVals) : "{}",
                NewValues = newVals != null ? JsonSerializer.Serialize(newVals) : "{}"
            });

            if (metadata.ProjectId.HasValue)
            {
                string? eventName = null;
                var flagKey = metadata.FriendlyName.Replace(" (Rule)", "");
                
                var isNewFlagTx = modifiedEntries
                    .Any(e => e is
                    {
                        Entity: FeatureFlag, 
                        State: EntityState.Added
                    });

                if (entry.Entity is FeatureFlag)
                {
                    if (entry.State == EntityState.Added)
                        eventName = "flag.created";
                    else if (entry.State == EntityState.Deleted)
                        eventName = "flag.deleted";
                    else if (entry.State == EntityState.Modified)
                        eventName = "flag.updated";
                }
                else if (entry is
                         {
                             Entity: FlagEnvironmentState, 
                             State: EntityState.Modified
                         } 
                         || entry.Entity is FlagRule 
                         && !isNewFlagTx)
                    eventName = "flag.updated";

                if (eventName != null)
                {
                    var existingEvent = webhookEvents.FirstOrDefault(e => 
                        e.ProjectId == metadata.ProjectId.Value && 
                        e.EventName == eventName && 
                        e.FlagKey == flagKey);

                    if (existingEvent != null)
                    {
                        if (existingEvent.EnvironmentId == null && metadata.EnvironmentId != null)
                        {
                            webhookEvents.Remove(existingEvent);
                            webhookEvents.Add(existingEvent with { EnvironmentId = metadata.EnvironmentId });
                        }
                    }
                    else
                    {
                        webhookEvents.Add(new WebhookEvent(
                            metadata.ProjectId.Value,
                            metadata.EnvironmentId,
                            eventName,
                            flagKey));
                    }
                }
            }
        }

        if (auditEntries.Count > 0)
            context.Set<AuditLog>().AddRange(auditEntries);

        if (webhookEvents.Count > 0)
            _webhookEventsTable.AddOrUpdate(context, webhookEvents);
    }
    
    private (Dictionary<string, object?>? oldVals, Dictionary<string, object?>? newVals) GetSnapshot(EntityEntry entry)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (name == "Id" 
                || name.EndsWith("Id") 
                || name.EndsWith("Hash")
                || name == "UpdatedAt"
                || name == "CreatedAt"
                || name == "LastTriggeredAt"
                || name == "ConsecutiveFailures") 
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