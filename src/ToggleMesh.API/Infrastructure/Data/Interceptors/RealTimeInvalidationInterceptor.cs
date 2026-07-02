using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Sse;
using ToggleMesh.API.Features.Segments.Domain;
using ToggleMesh.API.Features.Analytics.Domain;

namespace ToggleMesh.API.Infrastructure.Data.Interceptors;

public class RealTimeInvalidationInterceptor : SaveChangesInterceptor
{
    private readonly ISseService _sseService;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;

    private class ContextState
    {
        public List<object[]> SseKeys { get; } = [];
        public List<string> RedisKeysToDelete { get; } = [];
        public List<MemberSnapshot> MemberSnapshots { get; } = [];
    }

    private class MemberSnapshot
    {
        public Type Type { get; set; } = null!;
        public Guid ProjectId { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }
    }

    private readonly ConditionalWeakTable<DbContext, ContextState> _contextStates = new();

    public RealTimeInvalidationInterceptor(ISseService sseService, IConnectionMultiplexer redis,
        IMemoryCache memoryCache)
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
            else if (entry.Entity is FlagEnvironmentState flagState)
            {
                var projectId = flagState.FeatureFlag?.ProjectId
                                ?? flagState.Environment?.ProjectId;

                if (projectId == null)
                    projectId = eventData.Context.Set<FeatureFlag>()
                        .Where(f => f.Id == flagState.FeatureFlagId)
                        .Select(f => f.ProjectId)
                        .FirstOrDefault();

                if (projectId != null && projectId != Guid.Empty)
                {
                    state.SseKeys.Add(["projects", projectId, "flags"]);
                    state.SseKeys.Add(["projects", projectId, "experiments"]);
                }

                var flagKey = flagState.FeatureFlag?.Key;
                if (flagKey == null)
                    flagKey = eventData.Context.Set<FeatureFlag>()
                        .Where(f => f.Id == flagState.FeatureFlagId)
                        .Select(f => f.Key)
                        .FirstOrDefault();

                if (flagKey != null)
                {
                    state.SseKeys.Add(["environments", flagState.EnvironmentId, "flags", flagKey]);

                    var cacheKey = CacheKeys.FlagState(flagState.EnvironmentId, flagKey);
                    state.RedisKeysToDelete.Add(cacheKey);
                }
            }
            else if (entry.Entity is FlagRule flagRule)
            {
                var projectId = flagRule.FlagEnvironmentState?.FeatureFlag?.ProjectId
                                ?? flagRule.FlagEnvironmentState?.Environment?.ProjectId;

                if (projectId == null)
                    projectId = eventData.Context.Set<FlagEnvironmentState>()
                        .Where(s => s.Id == flagRule.FlagEnvironmentStateId)
                        .Select(s => s.FeatureFlag.ProjectId)
                        .FirstOrDefault();

                if (projectId != null && projectId != Guid.Empty)
                    state.SseKeys.Add(["projects", projectId, "flags"]);

                var envIdAndFlagKey = eventData.Context.Set<FlagEnvironmentState>()
                    .Where(s => s.Id == flagRule.FlagEnvironmentStateId)
                    .Select(s => new { s.EnvironmentId, s.FeatureFlag.Key })
                    .FirstOrDefault();

                if (envIdAndFlagKey != null)
                {
                    state.SseKeys.Add(["environments", envIdAndFlagKey.EnvironmentId, "flags", envIdAndFlagKey.Key]);
                    state.RedisKeysToDelete.Add(CacheKeys.FlagState(envIdAndFlagKey.EnvironmentId,
                        envIdAndFlagKey.Key));
                }
            }
            else if (entry.Entity is ContextualRollout rollout)
            {
                var projectId = (rollout.FlagEnvironmentState?.FeatureFlag?.ProjectId
                                 ?? rollout.FlagEnvironmentState?.Environment?.ProjectId) ?? eventData.Context.Set<FlagEnvironmentState>()
                    .Where(s => s.Id == rollout.FlagEnvironmentStateId)
                    .Select(s => s.FeatureFlag.ProjectId)
                    .FirstOrDefault();

                if (projectId != Guid.Empty)
                    state.SseKeys.Add(["projects", projectId, "flags"]);

                var envIdAndFlagKey = eventData.Context.Set<FlagEnvironmentState>()
                    .Where(s => s.Id == rollout.FlagEnvironmentStateId)
                    .Select(s => new { s.EnvironmentId, s.FeatureFlag.Key })
                    .FirstOrDefault();

                if (envIdAndFlagKey != null)
                {
                    state.SseKeys.Add(["environments", envIdAndFlagKey.EnvironmentId, "flags", envIdAndFlagKey.Key]);
                    state.RedisKeysToDelete.Add(CacheKeys.FlagState(envIdAndFlagKey.EnvironmentId,
                        envIdAndFlagKey.Key));
                }
            }
            else if (entry.Entity is Organization)
                state.SseKeys.Add(["organizations"]);
            else if (entry.Entity is OrganizationMember member)
                state.SseKeys.Add(["organizations", member.OrganizationId, "members"]);
            else if (entry.Entity is Webhook webhook)
                state.SseKeys.Add(["projects", webhook.ProjectId, "webhooks"]);
            else if (entry.Entity is ProjectEnvironment env)
                state.SseKeys.Add(["projects", env.ProjectId]);
            else if (entry.Entity is EnvironmentKey envKey)
            {
                var projectId = envKey.Environment?.ProjectId ?? eventData.Context.Set<ProjectEnvironment>()
                    .Where(e => e.Id == envKey.EnvironmentId)
                    .Select(e => e.ProjectId)
                    .FirstOrDefault();

                if (projectId != Guid.Empty)
                    state.SseKeys.Add(["projects", projectId, "environments", envKey.EnvironmentId, "keys"]);
            }
            else if (entry.Entity is Segment segment)
            {
                var projectId = segment.Environment?.ProjectId ?? eventData.Context.Set<ProjectEnvironment>()
                    .Where(e => e.Id == segment.EnvironmentId)
                    .Select(e => e.ProjectId)
                    .FirstOrDefault();

                if (projectId != Guid.Empty)
                    state.SseKeys.Add(["projects", projectId, "environments", segment.EnvironmentId, "segments"]);
            }
            else if (entry.Entity is SegmentRule sr)
            {
                var envId = sr.Segment?.EnvironmentId;
                var projectId = sr.Segment?.Environment?.ProjectId;

                if (envId == null || projectId == null)
                {
                    var info = eventData.Context.Set<Segment>()
                        .Where(s => s.Id == sr.SegmentId)
                        .Select(s => new { s.EnvironmentId, s.Environment.ProjectId })
                        .FirstOrDefault();

                    if (info != null)
                    {
                        envId = info.EnvironmentId;
                        projectId = info.ProjectId;
                    }
                }

                if (envId != null && projectId != null && projectId != Guid.Empty)
                    state.SseKeys.Add(["projects", projectId, "environments", envId, "segments"]);
            }
            else if (entry.Entity is ExperimentIteration iter)
            {
                var projectId = eventData.Context.Set<ProjectEnvironment>()
                    .Where(e => e.Id == iter.EnvironmentId)
                    .Select(e => e.ProjectId)
                    .FirstOrDefault();

                if (projectId != Guid.Empty)
                {
                    state.SseKeys.Add([
                        "projects", projectId, "environments", iter.EnvironmentId, "flags", iter.FlagKey, "experiments"
                    ]);
                    state.SseKeys.Add(["projects", projectId, "experiments"]);
                    state.SseKeys.Add(["projects", projectId, "experiments", "historical"]);
                }
            }
            else if (entry.Entity is ExperimentMetric metric)
            {
                var projectId = eventData.Context.Set<ProjectEnvironment>()
                    .Where(e => e.Id == metric.EnvironmentId)
                    .Select(e => e.ProjectId)
                    .FirstOrDefault();

                if (projectId != Guid.Empty)
                    state.SseKeys.Add([
                        "projects", projectId, "environments", metric.EnvironmentId, "flags", metric.FlagKey,
                        "experiments"
                    ]);
            }
            else if (entry.Entity is ContextualExperimentMetric cMetric)
            {
                var projectId = eventData.Context.Set<ProjectEnvironment>()
                    .Where(e => e.Id == cMetric.EnvironmentId)
                    .Select(e => e.ProjectId)
                    .FirstOrDefault();

                if (projectId != Guid.Empty)
                    state.SseKeys.Add([
                        "projects", projectId, "environments", cMetric.EnvironmentId, "flags", cMetric.FlagKey,
                        "experiments"
                    ]);
            }

            if (entry.Entity is ProjectMember pm)
            {
                state.SseKeys.Add(["projects", pm.ProjectId, "members"]);
                state.MemberSnapshots.Add(new MemberSnapshot
                {
                    Type = typeof(ProjectMember),
                    ProjectId = pm.ProjectId,
                    UserId = pm.UserId
                });
            }
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
            foreach (var snapshot in state.MemberSnapshots)
                if (snapshot.Type == typeof(ProjectMember))
                {
                    var cacheKey = CacheKeys.ProjectMemberState(snapshot.ProjectId, snapshot.UserId);
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
                        var cacheKey = CacheKeys.ProjectMemberState(projectId, snapshot.UserId);
                        _memoryCache.Remove(cacheKey);
                        await _redis.KeyDeleteAsync(cacheKey);
                    }
                }

        if (state.RedisKeysToDelete.Count > 0)
            foreach (var key in state.RedisKeysToDelete.Distinct())
                await _redis.KeyDeleteAsync(key);

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