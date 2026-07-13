using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Features.Client.SdkEvaluateFlag;
using ToggleMesh.API.Features.Client.SdkEvaluateFlags;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.Common.Contexts;
using ToggleMesh.Common.Rules;
using ToggleMesh.Common;
// ReSharper disable ForCanBeConvertedToForeach

namespace ToggleMesh.API.Features.Client.Domain;

public class SdkEvaluatorService : ISdkEvaluatorService
{
    private readonly AppDbContext _db;
    private readonly IRuleEngine _ruleEngine;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;
    private readonly IConfiguration _config;

    public SdkEvaluatorService(
        AppDbContext db, 
        IRuleEngine ruleEngine, 
        IConnectionMultiplexer redis, 
        IMemoryCache memoryCache,
        IConfiguration config)
    {
        _db = db;
        _ruleEngine = ruleEngine;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
        _config = config;
    }

    private static readonly string[] DefaultIdentityKeys 
        = ["UserId", "sub", "Email", "SessionId", "DeviceId", "Id"];
    
    public async Task<List<CompiledFlagState>> GetCompiledFlagsAsync(Guid envId, CancellationToken ct)
    {
        var memoryCacheKey = CacheKeys.SdkCompiledRules(envId);

        var cacheResult = await _memoryCache.GetOrCreateAsync(memoryCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var redisCacheKey = CacheKeys.SdkCompiledRules(envId);
            
            var redisValue = await _redis.StringGetAsync(redisCacheKey);
            List<FlagStateDto> dtoList;

            if (redisValue.HasValue)
                dtoList = JsonSerializer.Deserialize<List<FlagStateDto>>((string)redisValue!) ?? [];
            else
            {
                var states = await _db.FlagEnvironmentStates
                    .AsNoTracking()
                    .Include(x => x.FeatureFlag)
                        .ThenInclude(x => x.Variations)
                    .Include(x => x.Rules)
                    .Include(x => x.ContextualRollouts)
                    .Include(x => x.IndividualTargets)
                    .Where(x => x.EnvironmentId == envId)
                    .AsSplitQuery()
                    .ToListAsync(ct);

                dtoList = states.Select(x => new FlagStateDto(
                    x.FeatureFlag.Key,
                    x.IsEnabled,
                    x.OffVariationId,
                    x.FallthroughRollout.Select(r => new VariationWeight(r.VariationId, r.Weight)).ToArray(),
                    x.FeatureFlag.IsClientSideExposed,
                    x.Rules.Select(xx =>
                        new RuleDto(
                            xx.Priority,
                            xx.GroupId,
                            xx.Attribute,
                            xx.Operator,
                            xx.Value,
                            xx.Rollout.Select(r => new VariationWeight(r.VariationId, r.Weight)).ToArray())
                    ).ToList(),
                    x.ContextPartitionKeys,
                    x.ContextualRollouts.Count > 0 ? x.ContextualRollouts.ToDictionary(
                        c => c.ContextSlice, 
                        c => c.Rollout.Select(r => new VariationWeight(r.VariationId, r.Weight)).ToArray()) : null,
                    x.IndividualTargets.Count > 0 ? x.IndividualTargets.ToDictionary(t => t.IdentityKey, t => t.VariationId) : null,
                    x.FeatureFlag.Variations.ToDictionary(v => v.Id, v => v.Value),
                    x.IsExperimentActive
                )).ToList();

                var json = JsonSerializer.Serialize(dtoList);
                var ttl = TimeSpan.FromMinutes(_config.GetValue("Caching:DefaultTtlMinutes", 10));
                await _redis.StringSetAsync(redisCacheKey, json, ttl);
            }

            var result = new List<CompiledFlagState>(dtoList.Count);
            foreach (var dto in dtoList)
                result.Add(new CompiledFlagState(
                    dto.Key,
                    dto.IsEnabled,
                    dto.OffVariationId,
                    dto.FallthroughRollout,
                    dto.IsClientSideExposed,
                    _ruleEngine.CompileRules(dto.Rules),
                    dto.ContextPartitionKeys,
                    dto.ContextualRollouts,
                    dto.IndividualTargets,
                    dto.Variations,
                    dto.IsExperimentActive));

            return result;
        });

        return cacheResult ?? [];
    }

    public EvaluationResult? Evaluate(CompiledFlagState state, string identity, Dictionary<string, string> context)
    {
        var accessor = new ContextAccessor<Dictionary<string, string>>(context);
        var evalContext = new EvaluationContext<ContextAccessor<Dictionary<string, string>>>(
            accessor,
            [],
            DefaultIdentityKeys);

        var actualIdentity = evalContext.GetIdentity(identity);

        if (!state.IsEnabled)
        {
            if (state is { OffVariationId: not null, Variations: not null } && 
                state.Variations.TryGetValue(state.OffVariationId.Value, out var offVal))
                return new EvaluationResult(state.OffVariationId.Value, offVal);
            
            return null;
        }
        
        if (state.IndividualTargets != null && state.IndividualTargets.TryGetValue(actualIdentity, out var targetVarId))
        {
            if (state.Variations != null && state.Variations.TryGetValue(targetVarId, out var val))
            {
                return new EvaluationResult(targetVarId, val);
            }
        }

        var matchedIndex = _ruleEngine.Evaluate(state.Groups, ref evalContext);
        if (matchedIndex >= 0)
        {
            ref readonly var matchedGroup = ref state.Groups[matchedIndex];
            
            if (matchedGroup.FastResultVariationId.HasValue)
            {
                if (state.Variations != null && state.Variations.TryGetValue(matchedGroup.FastResultVariationId.Value, out var val))
                    return new EvaluationResult(matchedGroup.FastResultVariationId.Value, val);
            }
            else if (matchedGroup.Rollout is { Length: > 0 })
            {
                return EvaluateRollout(matchedGroup.Rollout, state, actualIdentity);
            }
        }

        var activeRollout = state.FallthroughRollout;
        
        if (state is { ContextualRollouts.Count: > 0, ContextPartitionKeys: not null })
        {
            try
            {
                var dict = new Dictionary<string, string?>();
                foreach (var key in state.ContextPartitionKeys)
                    dict[key] = context.TryGetValue(key, out var val) ? val : null;
                
                var sliceString = JsonSerializer.Serialize(dict);
                if (state.ContextualRollouts.TryGetValue(sliceString, out var contextualRollout))
                    activeRollout = contextualRollout;
            }
            catch
            {
                // ignore
            }
        }

        if (activeRollout.Length > 0)
            return EvaluateRollout(activeRollout, state, actualIdentity);

        return null;
    }

    private EvaluationResult? EvaluateRollout(VariationWeight[] rollout, CompiledFlagState state, string identity)
    {
        if (rollout.Length == 0) return null;
        
        var bucket = GetBucket(state.Key, identity);
        
        int currentLimit = 0;
        foreach (var r in rollout)
        {
            currentLimit += r.Weight;
            if (bucket < currentLimit)
            {
                if (state.Variations != null && state.Variations.TryGetValue(r.VariationId, out var val))
                {
                    return new EvaluationResult(r.VariationId, val);
                }
                break;
            }
        }
        
        return null;
    }

    private static int GetBucket(string flagKey, string identity)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;
        
        for (var i = 0; i < flagKey.Length; i++)
        {
            hash ^= flagKey[i];
            hash *= prime;
        }

        for (var i = 0; i < identity.Length; i++)
        {
            hash ^= identity[i];
            hash *= prime;
        }
        
        return (int)(hash % 10000);
    }
}
