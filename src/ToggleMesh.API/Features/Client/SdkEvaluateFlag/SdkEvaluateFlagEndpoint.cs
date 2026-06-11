using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Features.Client.SdkEvaluateFlags;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;
using ToggleMesh.API.Persistence;
using ToggleMesh.Common.Contexts;
using ToggleMesh.Common.Rules;

namespace ToggleMesh.API.Features.Client.SdkEvaluateFlag;

public class SdkEvaluateFlagEndpoint : ToggleEndpoint<SdkEvaluateFlagsRequest, SdkEvaluateFlagResponse>
{
    private readonly AppDbContext _db;
    private readonly IRuleEngine _ruleEngine;
    private readonly IDatabase _redis;
    private readonly IMemoryCache _memoryCache;
    private static readonly List<string> DefaultIdentityKeys 
        = ["UserId", "sub", "Email", "SessionId", "DeviceId", "Id"];

    public SdkEvaluateFlagEndpoint(AppDbContext db, IRuleEngine ruleEngine, IConnectionMultiplexer redis, IMemoryCache memoryCache)
    {
        _db = db;
        _ruleEngine = ruleEngine;
        _redis = redis.GetDatabase();
        _memoryCache = memoryCache;
    }

    public override void Configure()
    {
        Post("/sdk/evaluate/{flagKey}");
        Version(1);
        AllowAnonymous();
        PreProcessor<ApiKeyPreProcessor<SdkEvaluateFlagsRequest>>();
    }

    public override async Task HandleAsync(SdkEvaluateFlagsRequest req, CancellationToken ct)
    {
        var flagKey = Route<string>("flagKey");
        if (string.IsNullOrEmpty(flagKey))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var isClientSideRequest = req.KeyType == KeyType.Client;
        var memoryCacheKey = $"sdk:compiled_rules:{req.EnvId}";

        var compiledStates = await _memoryCache.GetOrCreateAsync(memoryCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            
            var cacheKey = $"sdk:flags:states:{req.EnvId}";
            List<FlagStateDto>? dtoList = null;
            var cachedValue = await _redis.StringGetAsync(cacheKey);

            if (cachedValue.HasValue)
            {
                dtoList = System.Text.Json.JsonSerializer.Deserialize<List<FlagStateDto>>((string)cachedValue!);
            }

            if (dtoList is null)
            {
                var states = await _db.FlagEnvironmentStates
                    .AsNoTracking()
                    .Include(x => x.FeatureFlag)
                    .Include(x => x.Rules)
                    .Where(x => x.EnvironmentId == req.EnvId)
                    .ToListAsync(ct);

                dtoList = states.Select(s => new FlagStateDto(
                    s.FeatureFlag.Key,
                    s.IsEnabled,
                    s.RolloutPercentage,
                    s.FeatureFlag.IsClientSideExposed,
                    s.Rules.Select(r => new RuleDto(r.GroupId, r.Attribute, r.Operator, r.Value)).ToList()
                )).ToList();

                await _redis.StringSetAsync(
                    cacheKey, 
                    System.Text.Json.JsonSerializer.Serialize(dtoList), 
                    TimeSpan.FromMinutes(10));
            }

            var result = new List<CompiledFlagState>(dtoList.Count);
            foreach (var dto in dtoList)
            {
                var groups = _ruleEngine.CompileRules(dto.Rules);
                result.Add(new CompiledFlagState(
                    dto.Key,
                    dto.IsEnabled,
                    dto.RolloutPercentage,
                    dto.IsClientSideExposed,
                    groups));
            }
            return result;
        });

        var state = compiledStates?.FirstOrDefault(x => x.Key == flagKey);

        if (state is null || (isClientSideRequest && !state.IsClientSideExposed))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!state.IsEnabled)
        {
            await Send.OkAsync(new SdkEvaluateFlagResponse(flagKey, false), ct);
            return;
        }

        var accessor = new ContextAccessor<Dictionary<string, string>>(req.Context);
        var evalContext = new EvaluationContext<ContextAccessor<Dictionary<string, string>>>(
            accessor, 
            [], 
            DefaultIdentityKeys);

        if (!_ruleEngine.Evaluate(state.Groups, ref evalContext))
        {
            await Send.OkAsync(new SdkEvaluateFlagResponse(flagKey, false), ct);
            return;
        }

        var actualIdentity = evalContext.GetIdentity(req.Identity);
        var result = RolloutEvaluator.Evaluate(state.RolloutPercentage, flagKey, actualIdentity);
        
        await Send.OkAsync(new SdkEvaluateFlagResponse(flagKey, result), ct);
    }
}

public record FlagStateDto(string Key, bool IsEnabled, int? RolloutPercentage, bool IsClientSideExposed, List<RuleDto> Rules);