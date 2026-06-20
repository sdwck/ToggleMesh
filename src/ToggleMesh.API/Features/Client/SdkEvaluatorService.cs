using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Features.Client.SdkEvaluateFlag;
using ToggleMesh.API.Features.Client.SdkEvaluateFlags;
using ToggleMesh.API.Persistence;
using ToggleMesh.Common.Contexts;
using ToggleMesh.Common.Rules;

namespace ToggleMesh.API.Features.Client;

public class SdkEvaluatorService : ISdkEvaluatorService
{
    private readonly AppDbContext _db;
    private readonly IRuleEngine _ruleEngine;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;

    public SdkEvaluatorService(
        AppDbContext db, 
        IRuleEngine ruleEngine, 
        IConnectionMultiplexer redis, 
        IMemoryCache memoryCache)
    {
        _db = db;
        _ruleEngine = ruleEngine;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
    }

    private static readonly List<string> DefaultIdentityKeys 
        = ["UserId", "sub", "Email", "SessionId", "DeviceId", "Id"];
    
    public async Task<List<CompiledFlagState>> GetCompiledFlagsAsync(Guid envId, CancellationToken ct)
    {
        var memoryCacheKey = $"sdk:compiled_rules:{envId}";

        var cacheResult = await _memoryCache.GetOrCreateAsync(memoryCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var redisCacheKey = $"sdk:flags:states:{envId}";
            
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
                    .Where(x => x.EnvironmentId == envId)
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
                    ).ToList()
                )).ToList();

                var json = System.Text.Json.JsonSerializer.Serialize(dtoList);
                await _redis.StringSetAsync(redisCacheKey, json, TimeSpan.FromMinutes(10));
            }

            var result = new List<CompiledFlagState>(dtoList.Count);
            foreach (var dto in dtoList)
                result.Add(new CompiledFlagState(
                    dto.Key,
                    dto.IsEnabled,
                    dto.RolloutPercentage,
                    dto.IsClientSideExposed,
                    _ruleEngine.CompileRules(dto.Rules)));

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
        return RolloutEvaluator.Evaluate(state.RolloutPercentage, state.Key, actualIdentity);
    }
}