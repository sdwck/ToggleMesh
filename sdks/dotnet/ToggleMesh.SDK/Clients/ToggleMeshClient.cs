using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
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

public class ToggleMeshClient : IToggleMeshClient, IHostedService
{
    private readonly ConcurrentDictionary<string, CachedFlag> _cache = new();
    private readonly HubConnection _connection;
    private readonly ILogger<ToggleMeshClient> _logger;
    private readonly HttpClient _client;
    private readonly IRuleEngine _ruleEngine;
    private readonly string? _fallbackFilePath;
    private readonly IToggleMeshContextProvider[] _contextProviders;
    private readonly List<string> _identityKeys = ["UserId", "sub", "Email", "SessionId", "DeviceId", "Id"];
    private ConcurrentDictionary<string, FlagMetrics> _metricsBuffer = new();
    private readonly bool _isMetricsEnabled;
    private readonly TimeProvider _timeProvider;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly CancellationTokenSource _sdkLifetimeCts = new();

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

        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(ex =>
                    ex.StatusCode == null || (int)ex.StatusCode >= 500 ||
                    ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout),
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(ex =>
                    ex.StatusCode == null || (int)ex.StatusCode >= 500 ||
                    ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout),
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

        var hubUrl = new Uri(
            new Uri(options.Value.BaseUrl), Constants.Endpoints.ToggleHub);

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, connectionOptions => { connectionOptions.Headers.Add("x-api-key", options.Value.ApiKey); })
            .WithAutomaticReconnect([
                TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            ])
            .Build();

        _connection.On<FeatureFlagDto>("FlagUpdated", flag =>
        {
            _logger.LogDebug("[ToggleMesh] Flag updated remotely: {Key} (IsEnabled: {Value}, Rules: {RuleCount})",
                flag.Key, flag.IsEnabled, flag.Rules.Count());

            CacheFlag(flag);
            _ = SaveFallbackAsync();
        });

        _connection.On("StateReloadRequired", async () =>
        {
            _logger.LogInformation("[ToggleMesh] Received state reload request from server.");
            await SyncStateWithApiAsync(CancellationToken.None);
        });

        _connection.Reconnected += async _ =>
        {
            _logger.LogInformation("[ToggleMesh] SignalR reconnected. Syncing state.");
            var jitterMs = Random.Shared.Next(100, 15000);
            await Task.Delay(TimeSpan.FromMilliseconds(jitterMs), _timeProvider);
            await SyncStateWithApiAsync(CancellationToken.None);
        };

        _connection.Closed += async error =>
        {
            if (_sdkLifetimeCts.IsCancellationRequested)
                return;
            
            _logger.LogWarning(error,
                "[ToggleMesh] Connection closed permanently. Restarting background connection loop.");
            await EnsureConnectedLoopAsync(_sdkLifetimeCts.Token);
        };
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

        if (!flag.IsEnabled || !_ruleEngine.Evaluate(flag.Groups, ref evalContext))
        {
            result = false;
        }
        else
        {
            var actualIdentity = evalContext.GetIdentity(identity);
            result = RolloutEvaluator.Evaluate(flag.RolloutPercentage, flagKey, actualIdentity);
        }

        UpdateMetrics(flagKey, result);
        return result;
    }

    private void CacheFlag(FeatureFlagDto flag)
    {
        _cache[flag.Key] = new CachedFlag
        {
            Key = flag.Key,
            IsEnabled = flag.IsEnabled,
            RolloutPercentage = flag.RolloutPercentage,
            Groups = _ruleEngine.CompileRules(flag.Rules),
            OriginalDto = flag
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
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            var oldBuffer = 
                Interlocked.Exchange(
                    ref _metricsBuffer, 
                    new ConcurrentDictionary<string, FlagMetrics>());
            if (oldBuffer.IsEmpty)
                continue;

            var payload = oldBuffer.Select(kvp => new
                {
                    kvp.Key,
                    TrueCount = Interlocked.Exchange(ref kvp.Value.TrueCount, 0),
                    FalseCount = Interlocked.Exchange(ref kvp.Value.FalseCount, 0)
                })
                .Where(x => x.TrueCount > 0 || x.FalseCount > 0)
                .ToList();

            if (payload.Count == 0)
                continue;

            var isSuccess = false;

            try
            {
                var response = await _resiliencePipeline.ExecuteAsync(async token =>
                    await _client.PostAsJsonAsync(Constants.Endpoints.Metrics, payload, token), ct);

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
                _logger.LogTrace(e, "[ToggleMesh] Error during metrics flush.");
            }
            finally
            {
                if (!isSuccess)
                    foreach (var item in payload)
                    {
                        var activeMetrics = _metricsBuffer.GetOrAdd(item.Key, _ => new FlagMetrics());
                        Interlocked.Add(ref activeMetrics.TrueCount, item.TrueCount);
                        Interlocked.Add(ref activeMetrics.FalseCount, item.FalseCount);
                    }

                foreach (var kvp in oldBuffer)
                {
                    var remainingTrue = Interlocked.Read(ref kvp.Value.TrueCount);
                    var remainingFalse = Interlocked.Read(ref kvp.Value.FalseCount);

                    if (remainingTrue > 0 || remainingFalse > 0)
                    {
                        var activeMetrics = _metricsBuffer.GetOrAdd(kvp.Key, _ => new FlagMetrics());
                        Interlocked.Add(ref activeMetrics.TrueCount, kvp.Value.TrueCount);
                        Interlocked.Add(ref activeMetrics.FalseCount, kvp.Value.FalseCount);
                    }
                }
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
            _ = Task.Run(() => RunMetricsFlusherAsync(_sdkLifetimeCts.Token), _sdkLifetimeCts.Token);
    }

    private async Task EnsureConnectedLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_connection.State == HubConnectionState.Disconnected)
                {
                    await _connection.StartAsync(ct);
                    _logger.LogTrace("[ToggleMesh] SignalR connection established.");
                }

                await SyncStateWithApiAsync(ct);
                break;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogCritical(
                    "[ToggleMesh] Invalid API Key. Background sync loop stopped permanently. Please check your configuration.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "[ToggleMesh] Background connection attempt failed. Retrying in 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, ct);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ToggleMesh] SDK stopping.");
        
        await _sdkLifetimeCts.CancelAsync();
        _sdkLifetimeCts.Dispose();
        
        await _connection.StopAsync(cancellationToken);
    }

    private async Task SyncStateWithApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            var flags = await _resiliencePipeline.ExecuteAsync(async token =>
                    await _client.GetFromJsonAsync<List<FeatureFlagDto>>(Constants.Endpoints.GetAll, token),
                cancellationToken);

            if (flags is null)
            {
                _logger.LogWarning("[ToggleMesh] API returned null flag data.");
                return;
            }

            var fetchedKeys = flags.Select(f => f.Key).ToHashSet();
            var keysToRemove = _cache.Keys.Where(k => !fetchedKeys.Contains(k)).ToList();

            foreach (var key in keysToRemove)
                _cache.TryRemove(key, out _);

            foreach (var flag in flags)
                CacheFlag(flag);

            _logger.LogInformation("[ToggleMesh] State synchronized with API. Loaded {Count} flags.", flags.Count);

            await SaveFallbackAsync();
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("[ToggleMesh] Circuit breaker is open. Cannot sync state right now.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogCritical("[ToggleMesh] Unauthorized (401). Invalid API Key.");
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
            var flags = JsonSerializer.Deserialize<List<FeatureFlagDto>>(content);

            if (flags != null)
            {
                foreach (var flag in flags)
                    CacheFlag(flag);
                _logger.LogInformation("[ToggleMesh] Loaded {Count} flags from offline fallback file.",
                    flags.Count);
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

            var payload = _cache.Values.Select(x => x.OriginalDto).ToList();
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
            // ignored
        }
    }
}