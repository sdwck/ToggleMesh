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
                    .Include(x => x.Rules)
                    .Include(x => x.ContextualRollouts)
                    .Where(x => x.EnvironmentId == envId)
                    .AsSplitQuery()
                    .ToListAsync(ct);

                dtoList = states.Select(x => new FlagStateDto(
                    x.FeatureFlag.Key,
                    x.IsEnabled,
                    x.RolloutPercentage,
                    x.FeatureFlag.IsClientSideExposed,
                    x.Rules.Select(xx =>
                        new RuleDto(
                            xx.GroupId,
                            xx.Attribute,
                            xx.Operator,
                            xx.Value)
                    ).ToList(),
                    x.ContextPartitionKeys,
                    x.ContextualRollouts?.ToDictionary(c => c.ContextSlice, c => c.RolloutPercentage),
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
                    dto.RolloutPercentage,
                    dto.IsClientSideExposed,
                    _ruleEngine.CompileRules(dto.Rules),
                    dto.ContextPartitionKeys,
                    dto.ContextualRollouts,
                    dto.IsExperimentActive));

            return result;
        });

        return cacheResult ?? [];
    }

    public bool Evaluate(CompiledFlagState state, string identity, Dictionary<string, string> context)
    {
        if (!state.IsEnabled)
            return false;
        
        var accessor = new ContextAccessor<Dictionary<string, string>>(context);
        var evalContext = new EvaluationContext<ContextAccessor<Dictionary<string, string>>>(
            accessor,
            [],
            DefaultIdentityKeys);

        if (!_ruleEngine.Evaluate(state.Groups, ref evalContext))
            return false;

        var actualIdentity = evalContext.GetIdentity(identity);
        
        int? activeRolloutPercentage = state.RolloutPercentage;
        
        if (state is { ContextualRollouts.Count: > 0, ContextPartitionKeys: not null })
        {
            try
            {
                var dict = new Dictionary<string, string?>();
                foreach (var key in state.ContextPartitionKeys)
                    dict[key] = context.TryGetValue(key, out var val) ? val : null;
                
                var sliceString = JsonSerializer.Serialize(dict);
                if (state.ContextualRollouts.TryGetValue(sliceString, out var contextualRollout))
                    activeRolloutPercentage = contextualRollout;
            }
            catch
            {
                // ignore
            }
        }

        return RolloutEvaluator.Evaluate(activeRolloutPercentage, state.Key, actualIdentity);
    }
}