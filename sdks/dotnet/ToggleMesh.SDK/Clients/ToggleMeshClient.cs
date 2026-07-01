using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
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
using ToggleMesh.Common.Rules;
using ToggleMesh.SDK.Models;
using ToggleMesh.SDK.Options;

namespace ToggleMesh.SDK.Clients;

public class ToggleMeshClient : IToggleMeshClient, IHostedService, ISegmentProvider
{
    private readonly ConcurrentDictionary<string, CachedFlag> _cache = new();
    private readonly ConcurrentDictionary<Guid, CachedSegment> _segmentsCache = new();
    private readonly ILogger<ToggleMeshClient> _logger;
    private readonly HttpClient _client;
    private readonly IRuleEngine _ruleEngine;
    private readonly string? _fallbackFilePath;
    private readonly IToggleMeshContextProvider[] _contextProviders;
    private readonly List<string> _identityKeys = ["UserId", "sub", "Email", "SessionId", "DeviceId", "Id"];
    private readonly ConcurrentDictionary<string, FlagMetrics> _metricsBuffer = new();
    private readonly Channel<AnalyticsEvent> _eventsChannel;
    private readonly bool _isMetricsEnabled;
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
        if (options.Value.IdentityKeys.Any())
            _identityKeys = options.Value.IdentityKeys.ToList();
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
                    _logger.LogWarning("[ToggleMesh] API is unreachable. Circuit breaker OPENED for 30s.");
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("[ToggleMesh] API recovered. Circuit breaker CLOSED.");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogDebug("[ToggleMesh] Circuit breaker HALF-OPENED. Testing API connection...");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        var safeKey = GetSafeFileName(options.Value.ApiKey);

        if (options.Value.UseFallbackFile)
            _fallbackFilePath = string.IsNullOrWhiteSpace(options.Value.FallbackFilePath)
                ? Path.Combine(AppContext.BaseDirectory, ".togglemesh", $"{safeKey}.json")
                : options.Value.FallbackFilePath;
    }

    private static string GetSafeFileName(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return "default";
        var bytes = System.Text.Encoding.UTF8.GetBytes(apiKey);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    public bool IsEnabled(string flagKey, bool defaultValue = false) =>
        IsEnabled<object>(flagKey, string.Empty, null!, defaultValue);

    public bool IsEnabled(string flagKey, string identity, bool defaultValue = false) =>
        IsEnabled<object>(flagKey, identity, null!, defaultValue);

    public bool IsEnabled(string flagKey, IDictionary<string, string> context, bool defaultValue = false) =>
        IsEnabled(flagKey, string.Empty, context, defaultValue);

    public bool IsEnabled(string flagKey, string identity, IDictionary<string, string> context,
        bool defaultValue = false) =>
        IsEnabled<IDictionary<string, string>>(flagKey, identity, context, defaultValue);

    public bool IsEnabled<TContext>(string flagKey, TContext contextObject, bool defaultValue = false) =>
        IsEnabled(flagKey, string.Empty, contextObject, defaultValue);

    public bool IsEnabled<TContext>(string flagKey, string identity, TContext contextObject, bool defaultValue = false)
    {
        if (!_cache.TryGetValue(flagKey, out var flag))
            return defaultValue;

        var accessor = new ContextAccessor<TContext>(contextObject);
        var evalContext = new EvaluationContext<ContextAccessor<TContext>>(accessor, _contextProviders, _identityKeys);

        bool result;

        var activeRolloutPercentage = flag.RolloutPercentage;

        if (flag is { ContextualRolloutsTree: not null, OriginalDto.ContextPartitionKeys: not null })
        {
            try
            {
                object? currentNode = flag.ContextualRolloutsTree;
                for (var i = 0; i < flag.OriginalDto.ContextPartitionKeys.Length; i++)
                {
                    var val = evalContext.TryGetValue(flag.OriginalDto.ContextPartitionKeys[i], out var v) ? v ?? "null" : "null";
                    if (currentNode is Dictionary<string, object> dict && dict.TryGetValue(val, out var nextNode))
                        currentNode = nextNode;
                    else
                    {
                        currentNode = null;
                        break;
                    }
                }
                
                if (currentNode is int percentage)
                    activeRolloutPercentage = percentage;
            }
            catch
            {
                // ignore
            }
        }

        if (!flag.IsEnabled || !_ruleEngine.Evaluate(flag.Groups, ref evalContext))
            result = false;
        else
        {
            var actualIdentity = evalContext.GetIdentity(identity);
            result = RolloutEvaluator.Evaluate(activeRolloutPercentage, flagKey, actualIdentity);
        }

        UpdateMetrics(flagKey, result);
        
        if (!string.IsNullOrEmpty(identity) && flag.IsExperimentActive)
        {
            var evt = ObjectPools<TContext>.Pool.Get();
            evt.Type = AnalyticsEventType.Exposure;
            evt.Timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            evt.Identity = identity;
            evt.FlagKey = flagKey;
            evt.Result = result;
            evt.Properties = contextObject;
            
            if (!_eventsChannel.Writer.TryWrite(evt))
                evt.ReturnToPool();
        }
        
        return result;
    }

    public bool IsEnabled<TContext>(string flagKey, ref TContext contextObject, bool defaultValue = false) where TContext : IContextAccessor =>
        IsEnabled(flagKey, string.Empty, ref contextObject, defaultValue);

    public bool IsEnabled<TContext>(string flagKey, string identity, ref TContext contextObject, bool defaultValue = false) where TContext : IContextAccessor
    {
        if (!_cache.TryGetValue(flagKey, out var flag))
            return defaultValue;

        var evalContext = new EvaluationContext<TContext>(contextObject, _contextProviders, _identityKeys);

        bool result;

        var activeRolloutPercentage = flag.RolloutPercentage;

        if (flag is { ContextualRolloutsTree: not null, OriginalDto.ContextPartitionKeys: not null })
        {
            try
            {
                object? currentNode = flag.ContextualRolloutsTree;
                for (var i = 0; i < flag.OriginalDto.ContextPartitionKeys.Length; i++)
                {
                    var val = evalContext.TryGetValue(flag.OriginalDto.ContextPartitionKeys[i], out var v) ? v ?? "null" : "null";
                    if (currentNode is Dictionary<string, object> dict && dict.TryGetValue(val, out var nextNode))
                        currentNode = nextNode;
                    else
                    {
                        currentNode = null;
                        break;
                    }
                }
                
                if (currentNode is int percentage)
                    activeRolloutPercentage = percentage;
            }
            catch
            {
                // ignore
            }
        }

        if (!flag.IsEnabled || !_ruleEngine.Evaluate(flag.Groups, ref evalContext))
            result = false;
        else
        {
            var actualIdentity = evalContext.GetIdentity(identity);
            result = RolloutEvaluator.Evaluate(activeRolloutPercentage, flagKey, actualIdentity);
        }

        UpdateMetrics(flagKey, result);
        
        if (!string.IsNullOrEmpty(identity) && flag.IsExperimentActive)
        {
            var evt = ObjectPools<TContext>.Pool.Get();
            evt.Type = AnalyticsEventType.Exposure;
            evt.Timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            evt.Identity = identity;
            evt.FlagKey = flagKey;
            evt.Result = result;
            evt.Properties = contextObject;
            
            if (!_eventsChannel.Writer.TryWrite(evt))
                evt.ReturnToPool();
        }
        
        return result;
    }


    public void Track(string eventName, double? value = null)
    {
        var accessor = new ContextAccessor<object>(null!);
        var evalContext = new EvaluationContext<ContextAccessor<object>>(accessor, _contextProviders, _identityKeys);
        var actualIdentity = evalContext.GetIdentity(string.Empty);
        Track(eventName, actualIdentity, value);
    }

    public void Track<TProperties>(string eventName, TProperties properties, double? value = null)
    {
        var accessor = new ContextAccessor<object>(null!);
        var evalContext = new EvaluationContext<ContextAccessor<object>>(accessor, _contextProviders, _identityKeys);
        var actualIdentity = evalContext.GetIdentity(string.Empty);
        Track(eventName, actualIdentity, properties, value);
    }

    public void Track<TContext, TProperties>(string eventName, TContext contextObject, TProperties properties, double? value = null)
    {
        var accessor = new ContextAccessor<TContext>(contextObject);
        var evalContext = new EvaluationContext<ContextAccessor<TContext>>(accessor, _contextProviders, _identityKeys);
        var actualIdentity = evalContext.GetIdentity(string.Empty);
        Track(eventName, actualIdentity, properties, value);
    }

    public void Track(string eventName, string identity, double? value = null)
    {
        if (string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(eventName))
            return;

        var evt = ObjectPools<object?>.Pool.Get();
        evt.Type = AnalyticsEventType.Track;
        evt.Timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        evt.Identity = identity;
        evt.EventName = eventName;
        evt.Value = value;
        evt.Properties = null;

        if (!_eventsChannel.Writer.TryWrite(evt))
            evt.ReturnToPool();
    }

    public void Track<TProperties>(string eventName, string identity, TProperties properties, double? value = null)
    {
        if (string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(eventName))
            return;

        var evt = ObjectPools<TProperties>.Pool.Get();
        evt.Type = AnalyticsEventType.Track;
        evt.Timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        evt.Identity = identity;
        evt.EventName = eventName;
        evt.Value = value;
        evt.Properties = properties;

        if (!_eventsChannel.Writer.TryWrite(evt))
            evt.ReturnToPool();
    }

    public bool IsEnabled<TContext>(string flagKey, ToggleMeshUser<TContext> user, bool defaultValue = false) where TContext : IContextAccessor
    {
        var context = user.Context;
        return IsEnabled(flagKey, user.Identity, ref context, defaultValue);
    }

    public void Track<TContext>(string eventName, ToggleMeshUser<TContext> user, double? value = null) where TContext : IContextAccessor
    {
        Track(eventName, user.Identity, value);
    }

    public void Track<TContext, TProperties>(string eventName, ToggleMeshUser<TContext> user, TProperties properties, double? value = null) where TContext : IContextAccessor
    {
        Track(eventName, user.Identity, properties, value);
    }

    private void CacheFlag(FeatureFlagDto flag)
    {
        Dictionary<string, int>? parsedRollouts = null;
        Dictionary<string, object>? rolloutsTree = null;

        if (flag.ContextualRollouts is { Count: > 0 } && flag.ContextPartitionKeys != null)
        {
            parsedRollouts = new Dictionary<string, int>(StringComparer.Ordinal);
            rolloutsTree = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kvp in flag.ContextualRollouts)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(kvp.Key);
                    if (dict == null) 
                        continue;
                    
                    var sliceKey = string.Join(
                        "|", 
                        flag.ContextPartitionKeys
                            .Select(k => dict.GetValueOrDefault(k, "null")));
                    parsedRollouts[sliceKey] = kvp.Value;
                    
                    var currentDict = rolloutsTree;
                    for (int i = 0; i < flag.ContextPartitionKeys.Length; i++)
                    {
                        var key = dict.GetValueOrDefault(flag.ContextPartitionKeys[i], "null");
                        if (i == flag.ContextPartitionKeys.Length - 1)
                        {
                            currentDict[key] = kvp.Value;
                        }
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

        _cache[flag.Key] = new CachedFlag
        {
            Key = flag.Key,
            IsEnabled = flag.IsEnabled,
            RolloutPercentage = flag.RolloutPercentage,
            ContextualRollouts = flag.ContextualRollouts,
            ParsedContextualRollouts = parsedRollouts,
            ContextualRolloutsTree = rolloutsTree,
            IsExperimentActive = flag.IsExperimentActive,
            Groups = _ruleEngine.CompileRules(flag.Rules),
            OriginalDto = flag
        };
    }

    public CompiledRuleGroup[]? GetSegmentRules(string segmentId)
    {
        if (Guid.TryParse(segmentId, out var id) && _segmentsCache.TryGetValue(id, out var segment))
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

    private void UpdateMetrics(string flagKey, bool result)
    {
        var metrics = _metricsBuffer.GetOrAdd(flagKey, _ => new FlagMetrics());
        if (result)
            Interlocked.Increment(ref metrics.TrueCount);
        else
            Interlocked.Increment(ref metrics.FalseCount);
    }

    private async Task RunMetricsFlusherAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10), _timeProvider);
        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            var payloadList = new List<(string Key, long TrueCount, long FalseCount)>();

            foreach (var kvp in _metricsBuffer)
            {
                var currentTrue = Interlocked.Exchange(ref kvp.Value.TrueCount, 0);
                var currentFalse = Interlocked.Exchange(ref kvp.Value.FalseCount, 0);

                if (currentTrue > 0 || currentFalse > 0)
                    payloadList.Add((kvp.Key, currentTrue, currentFalse));
            }

            if (payloadList.Count == 0)
                continue;

            var payload = payloadList.Select(x => 
                new { x.Key, x.TrueCount, x.FalseCount }).ToList();
            var isSuccess = false;

            try
            {
                var response = await _resiliencePipeline.ExecuteAsync(async token =>
                    await _client.PostAsJsonAsync(
                        Constants.Endpoints.Metrics, 
                        payload, 
                        token), 
                    ct);

                isSuccess = response.IsSuccessStatusCode;

                if (!isSuccess)
                    _logger.LogWarning(
                        "[ToggleMesh] Failed to flush metrics. Status: {StatusCode}",
                        response.StatusCode);
            }
            catch (BrokenCircuitException)
            {
                _logger.LogTrace("[ToggleMesh] Circuit breaker open. Skipping metrics flush.");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[ToggleMesh] Error during metrics flush.");
            }
            finally
            {
                if (!isSuccess)
                    foreach (var item in payloadList)
                    {
                        var activeMetrics = _metricsBuffer.GetOrAdd(item.Key, _ => new FlagMetrics());
                        Interlocked.Add(ref activeMetrics.TrueCount, item.TrueCount);
                        Interlocked.Add(ref activeMetrics.FalseCount, item.FalseCount);
                    }
            }
        }
    }

    private async Task RunAnalyticsFlusherAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10), _timeProvider);
        var batch = new List<AnalyticsEvent>(1000);

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

            while (_eventsChannel.Reader.TryRead(out var evt))
            {
                batch.Add(evt);
                if (batch.Count >= 1000)
                    break;
            }

            if (batch.Count == 0)
                continue;

            try
            {
                var response = await _resiliencePipeline.ExecuteAsync(async token =>
                    await _client.PostAsJsonAsync(Constants.Endpoints.Events, new { Events = batch }, token), ct);

                if (response.IsSuccessStatusCode)
                {
                    foreach (var evt in batch)
                        evt.ReturnToPool();
                    batch.Clear();
                }
                else
                    _logger.LogWarning("[ToggleMesh] Failed to flush analytics events. Status: {StatusCode}. Keeping {Count} events for next tick.", response.StatusCode, batch.Count);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[ToggleMesh] Error during analytics events flush. Keeping {Count} events for next tick.", batch.Count);
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ToggleMesh] SDK starting.");

        await LoadFallbackAsync();

        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(2), _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cts.Token,
            cancellationToken);

        try
        {
            await SyncStateWithApiAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[ToggleMesh] Initial sync timed out. Operating with offline cache/defaults.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ToggleMesh] Initial sync failed. Operating with offline cache/defaults.");
        }

        _ = Task.Run(() => EnsureConnectedLoopAsync(_sdkLifetimeCts.Token), _sdkLifetimeCts.Token);
        if (_isMetricsEnabled)
        {
            _ = Task.Run(() => RunMetricsFlusherAsync(_sdkLifetimeCts.Token), _sdkLifetimeCts.Token);
            _ = Task.Run(() => RunAnalyticsFlusherAsync(_sdkLifetimeCts.Token), _sdkLifetimeCts.Token);
        }
    }

        private async Task EnsureConnectedLoopAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) != 0) return;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await SyncStateWithApiAsync(ct);

                    using var request = new HttpRequestMessage(
                        HttpMethod.Get, Constants.Endpoints.SseStream);
                    using var response = await _client.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead, ct);
                    
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogCritical("[ToggleMesh] Invalid API Key. Background sync loop stopped permanently. Please check your configuration.");
                        _sdkLifetimeCts.Cancel();
                        break;
                    }
                    
                    response.EnsureSuccessStatusCode();

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
                        
                        var data = line.Substring(6);
                        var doc = JsonDocument.Parse(data);
                        if (!doc.RootElement.TryGetProperty("EventName", out var evtName)) 
                            continue;
                        
                        var eventName = evtName.GetString();
                        if (eventName == "FlagUpdated" && doc.RootElement.TryGetProperty("Payload", out var payload))
                        {
                            var flag = JsonSerializer.Deserialize<FeatureFlagDto>(payload.GetRawText());
                            if (flag == null) 
                                continue;
                            
                            _logger.LogDebug("[ToggleMesh] Flag updated remotely: {Key}", flag.Key);
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
                    _sdkLifetimeCts.Cancel();
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "[ToggleMesh] Background connection attempt failed. Retrying in 5s.");
                    await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, ct);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isConnecting, 0);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ToggleMesh] SDK stopping.");
        await _sdkLifetimeCts.CancelAsync();
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

            var fetchedKeys = flags.Select(f => f.Key).ToHashSet();
            var keysToRemove = _cache.Keys
                .Where(k => !fetchedKeys.Contains(k))
                .ToList();

            foreach (var key in keysToRemove)
                _cache.TryRemove(key, out _);

            foreach (var flag in flags)
                CacheFlag(flag);

            var fetchedSegmentIds = segments.Select(s => s.Id).ToHashSet();
            var segmentsToRemove = _segmentsCache.Keys
                .Where(k => !fetchedSegmentIds.Contains(k))
                .ToList();

            foreach (var key in segmentsToRemove)
                _segmentsCache.TryRemove(key, out _);

            foreach (var segment in segments)
                CacheSegment(segment);

            _logger.LogInformation("[ToggleMesh] State synchronized with API. Loaded {Count} flags and {SegCount} segments.", flags.Count, segments.Count);

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Failed to synchronize state with API.");
        }
    }

    private async Task LoadFallbackAsync()
    {
        try
        {
            if (_fallbackFilePath is null)
            {
                _logger.LogTrace("[ToggleMesh] Fallback file path not configured. Skipping offline fallback load.");
                return;
            }

            if (!File.Exists(_fallbackFilePath))
            {
                _logger.LogTrace("[ToggleMesh] No fallback file found at {Path}", _fallbackFilePath);
                return;
            }

            var content = await File.ReadAllTextAsync(_fallbackFilePath);
            var response = JsonSerializer.Deserialize<SdkGetFlagsResponse>(content);

            if (response != null)
            {
                foreach (var flag in response.Flags)
                    CacheFlag(flag);

                foreach (var segment in response.Segments)
                    CacheSegment(segment);

                _logger.LogInformation("[ToggleMesh] Loaded {Count} flags and {SegCount} segments from offline fallback file.",
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
        {
            _logger.LogTrace("[ToggleMesh] Fallback file path not configured. Skipping offline fallback save.");
            return;
        }

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
                Id = x.Id,
                Name = x.Name,
                Rules = x.Groups
                    .SelectMany(g => 
                        g.Rules.Select(r => new RuleDto(Array.IndexOf(x.Groups.ToArray(), g), r.Attribute, r.Operator.Name, r.CompiledValue?.ToString() ?? string.Empty)))
            }).ToList();
            
            var payload = new SdkGetFlagsResponse
            {
                Flags = flagsPayload,
                Segments = segmentsPayload
            };

            var content = JsonSerializer.Serialize(payload);
            await File.WriteAllTextAsync(tempFilePath, content);
            File.Move(tempFilePath, _fallbackFilePath, overwrite: true);
            _logger.LogTrace("[ToggleMesh] Fallback state saved successfully.");
        }
        catch (IOException)
        {
            _logger.LogTrace("[ToggleMesh] Fallback file is temporarily locked by another process.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ToggleMesh] Failed to write fallback file to {Path} due to an error.",
                _fallbackFilePath);
        }
        finally
        {
            TryDeleteTempFile(tempFilePath);
        }
    }
    
    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}