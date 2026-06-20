using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Features.Flags;
using ToggleMesh.API.Features.Organizations;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Webhooks;
using ToggleMesh.API.Infrastructure.Sse;

namespace ToggleMesh.API.Persistence.Interceptors;

public class RealTimeInvalidationInterceptor : SaveChangesInterceptor
{
    private readonly ISseService _sseService;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;

    private class ContextState
    {
        public List<object[]> SseKeys { get; } = new();
        public List<MemberSnapshot> MemberSnapshots { get; } = new();
    }

    private class MemberSnapshot
    {
        public Type Type { get; set; } = null!;
        public Guid ProjectId { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
    }

    private readonly ConditionalWeakTable<DbContext, ContextState> _contextStates = new();

    public RealTimeInvalidationInterceptor(ISseService sseService, IConnectionMultiplexer redis, IMemoryCache memoryCache)
    {
        _sseService = sseService;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context == null) 
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        
        var state = new ContextState();
            
        var entries = eventData.Context.ChangeTracker.Entries()
            .Where(e => 
                e.State 
                    is EntityState.Added
                    or EntityState.Modified
                    or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is Project)
                state.SseKeys.Add(["projects"]);
            else if (entry.Entity is FeatureFlag flag)
            {
                state.SseKeys.Add(["projects", flag.ProjectId, "flags"]);
                state.SseKeys.Add(["projects", flag.ProjectId, "tags"]);
            }
            else if (entry.Entity is Organization)
                state.SseKeys.Add(["organizations"]);
            else if (entry.Entity is OrganizationMember member)
                state.SseKeys.Add(["organizations", member.OrganizationId, "members"]);
            else if (entry.Entity is Webhook webhook)
                state.SseKeys.Add(["projects", webhook.ProjectId, "webhooks"]);

            if (entry.Entity is ProjectMember pm)
                state.MemberSnapshots.Add(new MemberSnapshot
                {
                    Type = typeof(ProjectMember),
                    ProjectId = pm.ProjectId,
                    UserId = pm.UserId
                });
            else if (entry.Entity is OrganizationMember om)
                state.MemberSnapshots.Add(new MemberSnapshot
                {
                    Type = typeof(OrganizationMember),
                    OrganizationId = om.OrganizationId,
                    UserId = om.UserId
                });
        }

        _contextStates.Remove(eventData.Context);
        _contextStates.Add(eventData.Context, state);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context == null ||
            !_contextStates.TryGetValue(eventData.Context, out var state))
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        
        _contextStates.Remove(eventData.Context);

        if (state.MemberSnapshots.Count > 0)
        {
            foreach (var snapshot in state.MemberSnapshots)
            {
                if (snapshot.Type == typeof(ProjectMember))
                {
                    var cacheKey = $"project-member-state:{snapshot.ProjectId}:{snapshot.UserId}";
                    _memoryCache.Remove(cacheKey);
                    await _redis.KeyDeleteAsync(cacheKey);
                }
                else if (snapshot.Type == typeof(OrganizationMember))
                {
                    var projectIds = await eventData.Context.Set<Project>()
                        .AsNoTracking()
                        .Where(p => p.OrganizationId == snapshot.OrganizationId)
                        .Select(p => p.Id)
                        .ToListAsync(cancellationToken);

                    foreach (var projectId in projectIds)
                    {
                        var cacheKey = $"project-member-state:{projectId}:{snapshot.UserId}";
                        _memoryCache.Remove(cacheKey);
                        await _redis.KeyDeleteAsync(cacheKey);
                    }
                }
            }
        }

        if (state.SseKeys.Count > 0)
        {
            var uniqueKeys = state.SseKeys
                .Select(k => JsonSerializer.Serialize(k))
                .Distinct()
                .Select(j => JsonSerializer.Deserialize<object[]>(j))
                .Where(k => k != null)
                .ToList();

            foreach (var key in uniqueKeys)
                await _sseService.BroadcastAsync("invalidate", new { queryKey = key });
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
