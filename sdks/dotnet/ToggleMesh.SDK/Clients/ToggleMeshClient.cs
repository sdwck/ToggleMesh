using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using ToggleMesh.Common;
using ToggleMesh.Common.Contexts;
using ToggleMesh.Common.Metrics;
using ToggleMesh.Common.Rules;
using ToggleMesh.SDK.Models;
using ToggleMesh.SDK.Options;

namespace ToggleMesh.SDK.Clients;

public class ToggleMeshClient : IToggleMeshClient, IHostedService, ISegmentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ConcurrentDictionary<string, CachedFlag> _cache = new();
    private readonly ConcurrentDictionary<Guid, CachedSegment> _segmentsCache = new();
    private readonly ILogger<ToggleMeshClient> _logger;
    private readonly HttpClient _client;
    private readonly IRuleEngine _ruleEngine;
    private readonly string? _fallbackFilePath;
    private readonly IToggleMeshContextProvider[] _contextProviders;
    private readonly string[] _identityKeys = ["UserId", "sub", "Email", "SessionId", "DeviceId", "Id"];
    private readonly ConcurrentDictionary<string, FlagMetrics> _metricsBuffer = new();
    private readonly Channel<AnalyticsEvent> _eventsChannel;
    private readonly bool _isMetricsEnabled;
    private readonly int _metricsBufferCapacity;
    private readonly int _maxBatchSize;
    private readonly TimeProvider _timeProvider;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly CancellationTokenSource _sdkLifetimeCts = new();
    private int _isConnecting;

    public ToggleMeshClient(
        IHttpClientFactory httpClientFactory,
        IOptions<ToggleMeshOptions> options,
        ILogger<ToggleMeshClient> logger,
        IRuleEngine ruleEngine,
        IEnumerable<IToggleMeshContextProvider> contextProviders,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _ruleEngine = ruleEngine;
        _contextProviders = contextProviders.ToArray();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _isMetricsEnabled = options.Value.IsMetricsEnabled;
        _metricsBufferCapacity = options.Value.MetricsBufferCapacity > 0 ? options.Value.MetricsBufferCapacity : 10000;
        _maxBatchSize = options.Value.MaxBatchSize > 0 ? options.Value.MaxBatchSize : 2000;
        if (options.Value.IdentityKeys.Any())
            _identityKeys = options.Value.IdentityKeys.ToArray();
        _client = httpClientFactory.CreateClient("ToggleMesh");
        
        var capacity = options.Value.AnalyticsChannelCapacity > 0 ? options.Value.AnalyticsChannelCapacity : 10000;
        _eventsChannel = Channel.CreateBounded<AnalyticsEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(ex =>
                    ex.StatusCode == null || (int)ex.StatusCode >= 500 ||
                    ex.StatusCode == HttpStatusCode.RequestTimeout),
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(ex =>
                    ex.StatusCode == null || (int)ex.StatusCode >= 500 ||
                    ex.StatusCode == HttpStatusCode.RequestTimeout),
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 2,
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = _ =>
                {
                    _logger.LogWarning("[ToggleMesh] API is unreachable. Circuit breaker OPENED for 30s."); return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("[ToggleMesh] API recovered. Circuit breaker CLOSED."); return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogDebug("[ToggleMesh] Circuit breaker HALF-OPENED. Testing API connection..."); return ValueTask.CompletedTask;
                }
            })
            .Build();

        var safeKey = GetSafeFileName(options.Value.ApiKey);

        if (options.Value.UseFallbackFile)
            _fallbackFilePath = string.IsNullOrWhiteSpace(options.Value.FallbackFilePath)
                ? Path.Combine(AppContext.BaseDirectory, ".togglemesh", $"{safeKey}.json")
                : options.Value.FallbackFilePath;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnabled(string flagKey, string? identity = null, bool defaultValue = false)
    {
        if (!_cache.TryGetValue(flagKey, out var flag)) 
            return defaultValue;
        if (string.IsNullOrEmpty(identity) && flag.HasFastPath)
        {
            if (_isMetricsEnabled) 
                Interlocked.Increment(ref flag.FastMetricsCount);
            return flag.FastBoolResult;
        }
        var result = EvaluateInternalWithFlag<EmptyContext>(
            flagKey, identity ?? string.Empty, default, null, flag);
        return GetBoolVariation(flag, result, defaultValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnabled<TContext>(string flagKey, TContext contextAttributes, bool defaultValue = false)
    {
        if (!_cache.TryGetValue(flagKey, out var flag)) 
            return defaultValue;
        if (flag.HasFastPath)
        {
            if (_isMetricsEnabled) 
                Interlocked.Increment(ref flag.FastMetricsCount);
            return flag.FastBoolResult;
        }
        var result = EvaluateInternalWithFlag(
            flagKey, string.Empty, contextAttributes, null, flag);
        return GetBoolVariation(flag, result, defaultValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnabled<TContext>(string flagKey, string? identity, TContext contextAttributes, bool defaultValue = false)
    {
        if (!_cache.TryGetValue(flagKey, out var flag)) 
            return defaultValue;
        if (string.IsNullOrEmpty(identity) && flag.HasFastPath)
        {
            if (_isMetricsEnabled) 
                Interlocked.Increment(ref flag.FastMetricsCount);
            return flag.FastBoolResult;
        }
        var result = EvaluateInternalWithFlag(
            flagKey, identity ?? string.Empty, contextAttributes, null, flag);
        return GetBoolVariation(flag, result, defaultValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnabled<TContext>(string flagKey, ref ToggleMeshUser<TContext> user, bool defaultValue = false) where TContext : IContextAccessor
    {
        if (!_cache.TryGetValue(flagKey, out var flag))
            return defaultValue;
        if (flag.HasFastPath)
        {
            if (_isMetricsEnabled)
                Interlocked.Increment(ref flag.FastMetricsCount);
            return flag.FastBoolResult;
        }
        
        var result = EvaluateInternalWithFlag(
            flagKey, user.Identity, ref user.Context, null, flag);
        return GetBoolVariation(flag, result, defaultValue);
    }

    
    public Guid? Evaluate(string flagKey, string? identity = null, Guid? defaultValue = null) =>
        EvaluateInternal<EmptyContext>(flagKey, identity ?? string.Empty, default, defaultValue, out _);
    
    public Guid? Evaluate<TContext>(string flagKey, TContext contextAttributes, Guid? defaultValue = null) =>
        EvaluateInternal(flagKey, string.Empty, contextAttributes, defaultValue, out _);

    public Guid? Evaluate<TContext>(string flagKey, string? identity, TContext contextAttributes, Guid? defaultValue = null) =>
        EvaluateInternal(flagKey, identity ?? string.Empty, contextAttributes, defaultValue, out _);

    public Guid? Evaluate<TContext>(string flagKey, ref ToggleMeshUser<TContext> user, Guid? defaultValue = null) where TContext : IContextAccessor
    {
        return EvaluateInternal(
            flagKey, user.Identity, ref user.Context, defaultValue, out _);
    }
    
    
    public string GetStringVariation(string flagKey, string? identity = null, string defaultValue = null!)
    {
        var result = EvaluateInternal<EmptyContext>(
            flagKey, identity ?? string.Empty, default, null, out var flag);
        return GetTypedVariation(flag, result, defaultValue);
    }
    
    public string GetStringVariation<TContext>(string flagKey, TContext contextAttributes, string defaultValue = null!)
    {
        var result = EvaluateInternal(
            flagKey, string.Empty, contextAttributes, null, out var flag);
        return GetTypedVariation(flag, result, defaultValue);
    }

    public string GetStringVariation<TContext>(string flagKey, string? identity, TContext? contextAttributes, string defaultValue = null!)
    {
        var result = EvaluateInternal(
            flagKey, identity ?? string.Empty, contextAttributes, null, out var flag);
        return GetTypedVariation(flag, result, defaultValue);
    }

    public string GetStringVariation<TContext>(string flagKey, ref ToggleMeshUser<TContext> user, string defaultValue = null!) where TContext : IContextAccessor
    {
        var result = EvaluateInternal(
            flagKey, user.Identity, ref user.Context, null, out var flag);
        return GetTypedVariation(flag, result, defaultValue);
    }

    
    public T GetJsonVariation<T>(string flagKey, string? identity = null, T defaultValue = default!)
    {
        var result = EvaluateInternal<EmptyContext>(
            flagKey, identity ?? string.Empty, default, null, out var flag);
        return GetTypedVariation(flag, result, defaultValue);
    }
    
    public T GetJsonVariation<TContext, T>(string flagKey, TContext contextAttributes, T defaultValue = default!)
    {
        var result = EvaluateInternal(
            flagKey, string.Empty, contextAttributes, null, out var flag);
        return GetTypedVariation(flag, result, defaultValue);
    }

    public T GetJsonVariation<TContext, T>(string flagKey, string? identity, TContext contextAttributes, T defaultValue = default!)
    {
        var result = EvaluateInternal(
            flagKey, identity ?? string.Empty, contextAttributes, null, out var flag);
        return GetTypedVariation(flag, result, defaultValue);
    }

    public T GetJsonVariation<TContext, T>(string flagKey, ref ToggleMeshUser<TContext> user, T defaultValue = default!) where TContext : IContextAccessor
    {
        var result = EvaluateInternal(
            flagKey, user.Identity, ref user.Context, null, out var flag);
        return GetTypedVariation(flag, result, defaultValue);
    }

    
    public void Track(string eventName, string? identity = null, double? value = null)
    {
        var accessor = new ContextAccessor<EmptyContext>(default);
        var evalContext = new EvaluationContext<ContextAccessor<EmptyContext>>(accessor, _contextProviders, _identityKeys);
        var actualIdentity = evalContext.GetIdentity(identity ?? string.Empty);

        if (string.IsNullOrEmpty(actualIdentity) || string.IsNullOrEmpty(eventName)) 
            return;

        var evt = ObjectPools<object?>.Pool.Get();
        evt.Type = AnalyticsEventType.Track;
        evt.Timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        evt.Identity = actualIdentity;
        evt.EventName = eventName;
        evt.Value = value;
        evt.Properties = null;
        if (!_eventsChannel.Writer.TryWrite(evt)) 
            evt.ReturnToPool();
    }
    
    public void Track<TProperties>(string eventName, TProperties properties, double? value = null)
    {
        var accessor = new ContextAccessor<TProperties>(properties);
        var evalContext = new EvaluationContext<ContextAccessor<TProperties>>(
            accessor, _contextProviders, _identityKeys);
        var actualIdentity = evalContext.GetIdentity(string.Empty);

        if (string.IsNullOrEmpty(actualIdentity) || string.IsNullOrEmpty(eventName)) 
            return;

        var evt = ObjectPools<TProperties>.Pool.Get();
        evt.Type = AnalyticsEventType.Track;
        evt.Timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        evt.Identity = actualIdentity;
        evt.EventName = eventName;
        evt.Value = value;
        evt.Properties = properties;
        if (!_eventsChannel.Writer.TryWrite(evt)) 
            evt.ReturnToPool();
    }

    public void Track<TProperties>(string eventName, string? identity, TProperties properties, double? value = null)
    {
        var accessor = new ContextAccessor<TProperties>(properties);
        var evalContext = new EvaluationContext<ContextAccessor<TProperties>>(
            accessor, _contextProviders, _identityKeys);
        var actualIdentity = evalContext.GetIdentity(identity ?? string.Empty);

        if (string.IsNullOrEmpty(actualIdentity) || string.IsNullOrEmpty(eventName)) 
            return;

        var evt = ObjectPools<TProperties>.Pool.Get();
        evt.Type = AnalyticsEventType.Track;
        evt.Timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        evt.Identity = actualIdentity;
        evt.EventName = eventName;
        evt.Value = value;
        evt.Properties = properties;
        if (!_eventsChannel.Writer.TryWrite(evt)) 
            evt.ReturnToPool();
    }
    
    public void Track<TContext>(string eventName, ref ToggleMeshUser<TContext> user, double? value = null) 
        where TContext : IContextAccessor
    {
        var evalContext = new EvaluationContext<TContext>(
            user.Context, _contextProviders, _identityKeys);
        var actualIdentity = evalContext.GetIdentity(user.Identity);

        if (string.IsNullOrEmpty(actualIdentity) || string.IsNullOrEmpty(eventName)) 
            return;

        var evt = ObjectPools<object?>.Pool.Get();
        evt.Type = AnalyticsEventType.Track;
        evt.Timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        evt.Identity = actualIdentity;
        evt.EventName = eventName;
        evt.Value = value;
        evt.Properties = null;
        if (!_eventsChannel.Writer.TryWrite(evt)) 
            evt.ReturnToPool();
    }

    public void Track<TContext, TProperties>(string eventName, ref ToggleMeshUser<TContext> user, ref TProperties properties, double? value = null) 
        where TContext : IContextAccessor 
        where TProperties : IContextAccessor
    {
        var evalContext = new EvaluationContext<TContext>(
            user.Context, _contextProviders, _identityKeys);
        var actualIdentity = evalContext.GetIdentity(user.Identity);

        if (string.IsNullOrEmpty(actualIdentity) || string.IsNullOrEmpty(eventName)) 
            return;

        var evt = ObjectPools<TProperties>.Pool.Get();
        evt.Type = AnalyticsEventType.Track;
        evt.Timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        evt.Identity = actualIdentity;
        evt.EventName = eventName;
        evt.Value = value;
        evt.Properties = properties;
        if (!_eventsChannel.Writer.TryWrite(evt)) 
            evt.ReturnToPool();
    }

    private static string GetSafeFileName(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) 
            return "default";
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    private Guid? EvaluateInternal<TContext>(string flagKey, string identity, TContext contextObject, Guid? defaultValue, out CachedFlag? evaluatedFlag)
    {
        if (!_cache.TryGetValue(flagKey, out evaluatedFlag)) 
            return defaultValue;
        var accessor = new ContextAccessor<TContext>(contextObject);
        return EvaluateInternalWithFlag(flagKey, identity, ref accessor, defaultValue, evaluatedFlag);
    }

    private Guid? EvaluateInternal<TContext>(string flagKey, string identity, ref TContext contextObject, Guid? defaultValue, out CachedFlag? evaluatedFlag) where TContext : IContextAccessor
    {
        if (!_cache.TryGetValue(flagKey, out evaluatedFlag)) 
            return defaultValue;
        return EvaluateInternalWithFlag(flagKey, identity, ref contextObject, defaultValue, evaluatedFlag);
    }

    private Guid? EvaluateInternalWithFlag<TContext>(string flagKey, string identity, TContext contextObject, Guid? defaultValue, CachedFlag flag)
    {
        var accessor = new ContextAccessor<TContext>(contextObject);
        return EvaluateInternalWithFlag(flagKey, identity, ref accessor, defaultValue, flag);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private VariationWeight[]? GetContextualRollout<TContext>(CachedFlag flag, ref EvaluationContext<TContext> evalContext) where TContext : IContextAccessor
    {
        try
        {
            object? currentNode = flag.ContextualRolloutsTree;
            for (var i = 0; i < flag.ContextPartitionKeys!.Length; i++)
            {
                var val = evalContext.TryGetValue(flag.ContextPartitionKeys[i], out var v) ? v ?? "null" : "null";
                if (currentNode is Dictionary<string, object> dict && dict.TryGetValue(val, out var nextNode))
                    currentNode = nextNode;
                else
                    return null;
            }

            if (currentNode is VariationWeight[] cr) 
                return cr;
        }
        catch
        {
             // ignore
        }
        return null;
    }

    private Guid? EvaluateInternalWithFlag<TContext>(string flagKey, string identity, ref TContext contextObject, Guid? defaultValue, CachedFlag flag) where TContext : IContextAccessor
    {
        if (flag.Strategy == EvaluationStrategy.Static)
        {
            UpdateMetrics(flagKey, flag.FastResultVariationId, flag);
            return flag.FastResultVariationId;
        }

        var evalContext = new EvaluationContext<TContext>(contextObject, _contextProviders, _identityKeys);
        var actualIdentity = identity;
        var isIdentityResolved = false;

        Guid? result;
        var activeRollout = flag.FallthroughRollout;

        switch (flag.Strategy)
        {
            case EvaluationStrategy.RolloutOnly:
                result = RolloutEvaluator.Evaluate(activeRollout, flag.OffVariationId, flagKey, GetActualIdentity(ref evalContext, identity, ref actualIdentity, ref isIdentityResolved));
                break;
            
            case EvaluationStrategy.RulesOnly:
                var matchIdx = _ruleEngine.Evaluate(flag.Groups, ref evalContext);
                if (matchIdx < 0) 
                    result = RolloutEvaluator.Evaluate(activeRollout, flag.OffVariationId, flagKey, GetActualIdentity(ref evalContext, identity, ref actualIdentity, ref isIdentityResolved));
                else 
                {
                    ref readonly var matchedGrp = ref flag.Groups[matchIdx];
                    result = matchedGrp.FastResultVariationId 
                             ?? RolloutEvaluator.Evaluate(matchedGrp.Rollout, flag.OffVariationId, flagKey, GetActualIdentity(ref evalContext, identity, ref actualIdentity, ref isIdentityResolved));
                }
                break;
            
            case EvaluationStrategy.Complex:
            default:
                if (flag.HasContextualRollouts)
                {
                    var contextualRollout = GetContextualRollout(flag, ref evalContext);
                    if (contextualRollout != null)
                        activeRollout = contextualRollout;
                }

                if (!flag.IsEnabled)
                    result = flag.OffVariationId;
                else if (flag.IndividualTargets != null && 
                         flag.IndividualTargets.TryGetValue(GetActualIdentity(ref evalContext, identity, ref actualIdentity, ref isIdentityResolved), out var individualVariationId))
                    result = individualVariationId;
                else
                {
                    if (flag.Groups.Length > 0)
                    {
                        var matchedIndex = _ruleEngine.Evaluate(flag.Groups, ref evalContext);
                        
                        if (matchedIndex < 0) 
                            result = RolloutEvaluator.Evaluate(activeRollout, flag.OffVariationId, flagKey, GetActualIdentity(ref evalContext, identity, ref actualIdentity, ref isIdentityResolved));
                        else
                        {
                            ref readonly var matchedGroup = ref flag.Groups[matchedIndex];
                            
                            if (matchedGroup.FastResultVariationId.HasValue)
                                result = matchedGroup.FastResultVariationId.Value;
                            else
                            {
                                if (matchedGroup.Rollout is { Length: > 0 })
                                    activeRollout = matchedGroup.Rollout;
                                
                                result = RolloutEvaluator.Evaluate(activeRollout, flag.OffVariationId, flagKey, GetActualIdentity(ref evalContext, identity, ref actualIdentity, ref isIdentityResolved));
                            }
                        }
                    }
                    else
                        result = RolloutEvaluator.Evaluate(activeRollout, flag.OffVariationId, flagKey, GetActualIdentity(ref evalContext, identity, ref actualIdentity, ref isIdentityResolved));
                }
                break;
        }

        UpdateMetrics(flagKey, result, flag);

        if (!flag.IsExperimentActive) 
            return result ?? defaultValue;
        
        actualIdentity = GetActualIdentity(ref evalContext, identity, ref actualIdentity, ref isIdentityResolved);
        if (string.IsNullOrEmpty(actualIdentity))
            return result ?? defaultValue;
        
        var evt = ObjectPools<TContext>.Pool.Get();
        evt.Type = AnalyticsEventType.Exposure;
        evt.Timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        evt.Identity = actualIdentity;
        evt.FlagKey = flagKey;
        evt.Result = result;
        evt.VariationId = result;
        if (result.HasValue && flag.Variations != null && flag.Variations.TryGetValue(result.Value, out var variationValue))
            evt.VariationValue = variationValue;
        evt.Properties = contextObject;
            
        if (!_eventsChannel.Writer.TryWrite(evt))
            evt.ReturnToPool();

        return result ?? defaultValue;

        static string GetActualIdentity(ref EvaluationContext<TContext> ctx, string originalIdentity, ref string? currentIdentity, ref bool isResolved)
        {
            if (isResolved) 
                return currentIdentity ?? string.Empty;
            if (string.IsNullOrEmpty(currentIdentity))
                currentIdentity = ctx.GetIdentity(originalIdentity);
            isResolved = true;
            return currentIdentity;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool GetBoolVariation(CachedFlag flag, Guid? variationId, bool defaultValue)
    {
        if (!variationId.HasValue) 
            return defaultValue;
        if (flag.TrueVariationId.HasValue)
            return variationId.Value == flag.TrueVariationId.Value;
        if (flag.Variations == null || 
            !flag.Variations.TryGetValue(variationId.Value, out var val)) 
            return defaultValue;
        return val.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private T GetTypedVariation<T>(CachedFlag? flag, Guid? variationId, T defaultValue)
    {
        if (variationId == null || flag == null) 
            return defaultValue;
        if (flag.Variations == null || 
            !flag.Variations.TryGetValue(variationId.Value, out var val)) 
            return defaultValue;

        try
        {
            if (typeof(T) == typeof(bool)) 
                return (T)(object)bool.Parse(val);
            if (typeof(T) == typeof(string)) 
                return (T)(object)val;

            if (flag.ParsedJsonVariations.TryGetValue(variationId.Value, out var cachedObj))
                if (cachedObj is T cachedTyped)
                    return cachedTyped;

            var deserialized = JsonSerializer.Deserialize<T>(val, JsonOptions);
            if (deserialized != null)
            {
                flag.ParsedJsonVariations[variationId.Value] = deserialized;
                return deserialized;
            }

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private void CacheFlag(FeatureFlagDto flag)
    {
        Dictionary<string, object>? rolloutsTree = null;

        if (flag is { ContextualRollouts.Count: > 0, ContextPartitionKeys: not null })
        {
            rolloutsTree = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kvp in flag.ContextualRollouts)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(kvp.Key);
                    if (dict == null) 
                        continue;

                    var currentDict = rolloutsTree;
                    for (var i = 0; i < flag.ContextPartitionKeys.Length; i++)
                    {
                        var key = dict.GetValueOrDefault(flag.ContextPartitionKeys[i], "null");
                        if (i == flag.ContextPartitionKeys.Length - 1)
                            currentDict[key] = kvp.Value.ToArray();
                        else
                        {
                            if (!currentDict.TryGetValue(key, out var nextNode))
                            {
                                nextNode = new Dictionary<string, object>(StringComparer.Ordinal);
                                currentDict[key] = nextNode;
                            }

                            currentDict = (Dictionary<string, object>)nextNode;
                        }
                    }
                }
                catch
                {
                     // ignore
                }
            }
        }

        var groups = _ruleEngine.CompileRules(flag.Rules);
        var rollout = flag.FallthroughRollout?.ToArray() ?? [];
        
        var cached = new CachedFlag
        {
            Key = flag.Key,
            IsEnabled = flag.IsEnabled,
            OffVariationId = flag.OffVariationId,
            FallthroughRollout = rollout,
            IndividualTargets = flag.IndividualTargets,
            ContextualRolloutsTree = rolloutsTree,
            HasContextualRollouts = rolloutsTree != null && 
                                    flag.ContextPartitionKeys is { Length: > 0 },
            ContextPartitionKeys = flag.ContextPartitionKeys,
            IsExperimentActive = flag.IsExperimentActive,
            Variations = flag.Variations,
            Groups = groups,
            OriginalDto = flag
        };

        var hasTargets = flag.IndividualTargets is { Count: > 0 };
        if (cached is { IsEnabled: true, IsExperimentActive: false } 
            && groups.Length == 0 
            && rolloutsTree == null 
            && !hasTargets)
        {
            if (rollout is [{ Weight: >= 10000 }])
            {
                cached.Strategy = EvaluationStrategy.Static;
                cached.FastResultVariationId = rollout[0].VariationId;
                cached.HasFastPath = true;
                
                if (cached.Variations != null && cached.Variations.TryGetValue(rollout[0].VariationId, out var boolStr))
                    cached.FastBoolResult = boolStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            else
                cached.Strategy = EvaluationStrategy.RolloutOnly;
        }
        else
            cached.Strategy = EvaluationStrategy.Complex;

        if (flag.Variations != null)
        {
            foreach (var kvp in flag.Variations)
            {
                if (kvp.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    cached.TrueVariationId = kvp.Key;
                    break;
                }
            }
        }

        _cache[flag.Key] = cached;

        if (_isMetricsEnabled)
        {
            var metrics = _metricsBuffer.GetOrAdd(cached.Key, _ => new FlagMetrics());
            if (cached.FastResultVariationId.HasValue)
                metrics.Slot0Id = cached.FastResultVariationId.Value;
            cached.Metrics = metrics;
        }
    }

    public CompiledRuleGroup[]? GetSegmentRules(string segmentId)
    {
        if (Guid.TryParse(segmentId, out var id) && 
            _segmentsCache.TryGetValue(id, out var segment))
            return segment.Groups.ToArray();
        return null;
    }

    private void CacheSegment(SegmentDto segment)
    {
        _segmentsCache[segment.Id] = new CachedSegment
        {
            Id = segment.Id,
            Name = segment.Name,
            Groups = _ruleEngine.CompileRules(segment.Rules)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateMetrics(string flagKey, Guid? result, CachedFlag flag)
    {
        if (!_isMetricsEnabled || result == null) 
            return;

        var metrics = flag.Metrics;
        if (metrics == null)
        {
            if (_metricsBuffer.Count >= _metricsBufferCapacity) 
                return;
            metrics = _metricsBuffer.GetOrAdd(flagKey, _ => new FlagMetrics());
            flag.Metrics = metrics;
        }

        metrics.Increment(result.Value);
    }

    private async Task RunMetricsFlusherAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10), _timeProvider);
            while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
                await FlushMetricsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Metrics flusher loop crashed unexpectedly.");
        }
    }

    private async Task FlushMetricsAsync(CancellationToken ct)
    {
        foreach (var flag in _cache.Values)
        {
            if (flag is not { HasFastPath: true, FastMetricsCount: > 0, FastResultVariationId: not null }) 
                continue;
            
            var count = Interlocked.Exchange(ref flag.FastMetricsCount, 0);
            if (count <= 0) 
                continue;
                
            var m = _metricsBuffer.GetOrAdd(flag.Key, _ => new FlagMetrics());
            m.AddCount(flag.FastResultVariationId.Value, count);
        }
        
        var payloadList = new List<MetricPayload>();

        foreach (var kvp in _metricsBuffer)
        {
            var flagKey = kvp.Key;
            var m = kvp.Value;
            var variationPayloads = new List<MetricVariationPayload>();

            if (m.Slot0Id != Guid.Empty)
            {
                var count = Interlocked.Exchange(ref m.Slot0Count, 0);
                if (count > 0) 
                    variationPayloads.Add(new MetricVariationPayload(m.Slot0Id, count));
            }
            if (m.Slot1Id != Guid.Empty)
            {
                var count = Interlocked.Exchange(ref m.Slot1Count, 0);
                if (count > 0) 
                    variationPayloads.Add(new MetricVariationPayload(m.Slot1Id, count));
            }
            if (m.Overflow != null)
            {
                foreach (var (key, count) in m.Overflow)
                {
                    if (count <= 0) 
                        continue;
                    
                    m.Overflow.AddOrUpdate(key, 0, (_, current) => current - count);
                    variationPayloads.Add(new MetricVariationPayload(key, count));
                }
            }

            if (variationPayloads.Count > 0)
                payloadList.Add(new MetricPayload(flagKey, variationPayloads));
        }

        if (payloadList.Count == 0) 
            return;

        var isSuccess = false;

        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(async token =>
                await _client.PostAsJsonAsync(Constants.Endpoints.Metrics, payloadList, token), ct);

            isSuccess = response.IsSuccessStatusCode;
            if (!isSuccess)
                _logger.LogWarning("[ToggleMesh] Failed to flush metrics. Status: {StatusCode}", response.StatusCode);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogTrace("[ToggleMesh] Circuit breaker open. Skipping metrics flush.");
        }
        catch (HttpRequestException e)
        {
            _logger.LogWarning("[ToggleMesh] Network error during metrics flush: {Message}", e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ToggleMesh] Unexpected error during metrics flush.");
        }
        finally
        {
            if (!isSuccess)
                foreach (var item in payloadList)
                {
                    var activeMetrics = _metricsBuffer.GetOrAdd(item.Key, _ => new FlagMetrics());
                    foreach (var v in item.Variations) 
                        activeMetrics.AddCount(v.VariationId, v.Count);
                }
        }
    }

    private async Task RunAnalyticsFlusherAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10), _timeProvider);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await FlushAnalyticsAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Analytics flusher loop crashed unexpectedly.");
        }
    }

    private async Task FlushAnalyticsAsync(CancellationToken ct)
    {
        var batch = new List<AnalyticsEvent>(_maxBatchSize);

        while (_eventsChannel.Reader.TryRead(out var evt))
        {
            batch.Add(evt);
            if (batch.Count >= _maxBatchSize) break;
        }

        if (batch.Count == 0) 
            return;

        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(async token =>
                await _client.PostAsJsonAsync(Constants.Endpoints.Events, new { Events = batch }, token), ct);

            if (response.IsSuccessStatusCode)
            {
                foreach (var evt in batch) evt.ReturnToPool();
                batch.Clear();
            }
            else
                _logger.LogWarning(
                    "[ToggleMesh] Failed to flush analytics events. Status: {StatusCode}. Keeping {Count} events for next tick.",
                    response.StatusCode, batch.Count);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogTrace(
                "[ToggleMesh] Circuit breaker open. Skipping analytics flush. Keeping {Count} events for next tick.",
                batch.Count);
        }
        catch (HttpRequestException e)
        {
            _logger.LogWarning(
                "[ToggleMesh] Network error during analytics flush. Keeping {Count} events for next tick. ({Message})",
                batch.Count, e.Message);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[ToggleMesh] Unexpected error during analytics flush. Keeping {Count} events for next tick.", batch.Count);
        }
        finally
        {
            if (batch.Count > 0)
                foreach (var evt in batch)
                    if (!_eventsChannel.Writer.TryWrite(evt)) evt.ReturnToPool();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ToggleMesh] SDK starting.");
        await LoadFallbackAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2), _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        try
        {
            await SyncStateWithApiAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[ToggleMesh] Initial sync timed out. Operating with offline cache/defaults.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning(
                "[ToggleMesh] Initial API connection failed: {Message}. Operating with offline cache/defaults.",
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ToggleMesh] Initial sync failed unexpectedly. Operating with offline cache/defaults.");
        }

        _ = Task.Run(() => EnsureConnectedLoopAsync(_sdkLifetimeCts.Token), _sdkLifetimeCts.Token);
        _ = Task.Run(() => RunAnalyticsFlusherAsync(_sdkLifetimeCts.Token), _sdkLifetimeCts.Token);

        if (_isMetricsEnabled)
            _ = Task.Run(() => RunMetricsFlusherAsync(_sdkLifetimeCts.Token), _sdkLifetimeCts.Token);
    }

    private async Task EnsureConnectedLoopAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) != 0) 
            return;
        var backoff = 1.0;

        try
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await SyncStateWithApiAsync(ct);

                        using var request = new HttpRequestMessage(HttpMethod.Get, Constants.Endpoints.SseStream);
                        using var response =
                            await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            _logger.LogCritical(
                                "[ToggleMesh] Invalid API Key. Background sync loop stopped permanently. Please check your configuration.");
                            await _sdkLifetimeCts.CancelAsync();
                            break;
                        }

                        response.EnsureSuccessStatusCode();
                        backoff = 1.0;

                        await using var stream = await response.Content.ReadAsStreamAsync(ct);
                        using var reader = new StreamReader(stream);

                        while (!ct.IsCancellationRequested)
                        {
                            var line = await reader.ReadLineAsync(ct);
                            if (line == null) 
                                break;
                            if (string.IsNullOrWhiteSpace(line)) 
                                continue;
                            if (!line.StartsWith("data: ")) 
                                continue;

                            var data = line[6..];
                            var doc = JsonDocument.Parse(data);
                            if (!doc.RootElement.TryGetProperty("EventName", out var evtName)) 
                                continue;

                            var eventName = evtName.GetString();
                            if (eventName == "SdkFlagUpdated" &&
                                doc.RootElement.TryGetProperty("Payload", out var payload))
                            {
                                var flag = JsonSerializer.Deserialize<FeatureFlagDto>(
                                    payload.GetRawText());
                                if (flag == null) 
                                    continue;
                                _logger.LogInformation("[ToggleMesh] Flag updated remotely: {Key}", flag.Key);
                                CacheFlag(flag);
                                _ = SaveFallbackAsync();
                            }
                            else if (eventName == "StateReloadRequired")
                            {
                                _logger.LogInformation("[ToggleMesh] Received state reload request from server.");
                                await SyncStateWithApiAsync(ct);
                            }
                        }
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogCritical("[ToggleMesh] Invalid API Key. Background sync loop stopped permanently.");
                        await _sdkLifetimeCts.CancelAsync();
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex,
                            "[ToggleMesh] Background connection attempt failed. Retrying in {backoff}s.", backoff);
                    }

                    if (ct.IsCancellationRequested) 
                        break;

                    var jitter = Random.Shared.NextDouble();
                    var waitTime = TimeSpan.FromSeconds(backoff + jitter);
                    await Task.Delay(waitTime, _timeProvider, ct);

                    backoff = Math.Min(backoff * 2, 30.0);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isConnecting, 0);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Background connection loop crashed unexpectedly.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ToggleMesh] SDK stopping.");
        await _sdkLifetimeCts.CancelAsync();

        if (_isMetricsEnabled)
        {
            try
            {
                await FlushMetricsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ToggleMesh] Failed to flush final metrics on shutdown.");
            }
        }

        try
        {
            var limit = 10;
            while (_eventsChannel.Reader.Count > 0 && limit-- > 0)
                await FlushAnalyticsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ToggleMesh] Failed to flush final analytics events on shutdown.");
        }

        _sdkLifetimeCts.Dispose();
    }

    private async Task SyncStateWithApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(async token =>
                    await _client.GetFromJsonAsync<SdkGetFlagsResponse>(
                        Constants.Endpoints.GetAll, token),
                cancellationToken);

            if (response is null)
            {
                _logger.LogWarning("[ToggleMesh] API returned null flag data.");
                return;
            }

            var flags = response.Flags;
            var segments = response.Segments;

            var fetchedKeys = flags
                .Select(f => f.Key)
                .ToHashSet();
            var keysToRemove = _cache.Keys
                .Where(k => !fetchedKeys.Contains(k))
                .ToList();
            foreach (var key in keysToRemove)
                _cache.TryRemove(key, out _);
            foreach (var flag in flags)
                CacheFlag(flag);

            var fetchedSegmentIds = segments
                .Select(s => s.Id)
                .ToHashSet();
            var segmentsToRemove = _segmentsCache.Keys
                .Where(k => !fetchedSegmentIds.Contains(k))
                .ToList();
            foreach (var key in segmentsToRemove) 
                _segmentsCache.TryRemove(key, out _);
            foreach (var segment in segments) 
                CacheSegment(segment);

            _logger.LogInformation(
                "[ToggleMesh] State synchronized with API. Loaded {Count} flags and {SegCount} segments.", flags.Count,
                segments.Count);
            await SaveFallbackAsync();
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("[ToggleMesh] Circuit breaker is open. Cannot sync state right now.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogCritical("[ToggleMesh] Unauthorized (401). Invalid API Key.");
            throw;
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (HttpRequestException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("[ToggleMesh] Failed to synchronize state with API: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Unexpected error during state synchronization.");
        }
    }

    private async Task LoadFallbackAsync()
    {
        try
        {
            if (_fallbackFilePath is null)
                return;
            if (!File.Exists(_fallbackFilePath))
                return;

            var content = await File.ReadAllTextAsync(_fallbackFilePath);
            var response = JsonSerializer.Deserialize<SdkGetFlagsResponse>(content);

            if (response != null)
            {
                foreach (var flag in response.Flags) CacheFlag(flag);
                foreach (var segment in response.Segments) CacheSegment(segment);
                _logger.LogInformation(
                    "[ToggleMesh] Loaded {Count} flags and {SegCount} segments from offline fallback file.",
                    response.Flags.Count, response.Segments.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Failed to read fallback file at {Path}", _fallbackFilePath);
        }
    }

    private async Task SaveFallbackAsync()
    {
        if (_fallbackFilePath is null) 
            return;
        var tempFilePath = $"{_fallbackFilePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            var dir = Path.GetDirectoryName(_fallbackFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) 
                Directory.CreateDirectory(dir);

            var flagsPayload = _cache.Values
                .Select(x => x.OriginalDto)
                .ToList();
            var segmentsPayload = _segmentsCache.Values
                .Select(x => new SegmentDto
            {
                Id = x.Id, Name = x.Name,
                Rules = x.Groups.SelectMany(g => g.Rules.Select(r => new RuleDto(0,
                    Array.IndexOf(x.Groups.ToArray(), g), r.Attribute, r.Operator.Name,
                    r.CompiledValue?.ToString() ?? string.Empty)))
            }).ToList();

            var payload = new SdkGetFlagsResponse
            {
                Flags = flagsPayload, 
                Segments = segmentsPayload
            };
            var content = JsonSerializer.Serialize(payload);
            await File.WriteAllTextAsync(tempFilePath, content);
            File.Move(tempFilePath, _fallbackFilePath, overwrite: true);
        }
        catch (IOException)
        {
            _logger.LogTrace("[ToggleMesh] Fallback file is temporarily locked by another process.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Failed to write fallback file to {Path} due to an error.",
                _fallbackFilePath);
        }
        finally
        {
            TryDeleteTempFile(tempFilePath);
        }
    }
    
    private static void TryDeleteTempFile(string path) {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
             // ignore
        } 
    }
    
    private record MetricVariationPayload(Guid VariationId, long Count);
    private record MetricPayload(string Key, List<MetricVariationPayload> Variations);
    public readonly struct EmptyContext;
}