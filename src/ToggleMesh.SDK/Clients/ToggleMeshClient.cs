using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToggleMesh.SDK.Contexts;
using ToggleMesh.SDK.Models;
using ToggleMesh.SDK.Options;
using ToggleMesh.SDK.Rules;

namespace ToggleMesh.SDK.Clients;

public class ToggleMeshClient : IToggleMeshClient, IHostedService
{
    private readonly ConcurrentDictionary<string, FeatureFlagDto> _cache = new();
    private readonly HubConnection _connection;
    private readonly ILogger<ToggleMeshClient> _logger;
    private readonly HttpClient _client;
    private readonly IRuleEngine _ruleEngine;
    private readonly string? _fallbackFilePath;
    private readonly IEnumerable<IToggleMeshContextProvider> _contextProviders;
    private readonly List<string> _identityKeys = ["UserId", "Email", "SessionId", "DeviceId", "Id"];
    private ConcurrentDictionary<string, FlagMetrics> _metricsBuffer = new();
    private readonly bool _isMetricsEnabled;

    private static string GetSafeFileName(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return "default";
        var bytes = System.Text.Encoding.UTF8.GetBytes(apiKey);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    public ToggleMeshClient(
        IHttpClientFactory httpClientFactory,
        IOptions<ToggleMeshOptions> options, 
        ILogger<ToggleMeshClient> logger,
        IRuleEngine ruleEngine, 
        IEnumerable<IToggleMeshContextProvider> contextProviders)
    {
        _logger = logger;
        _ruleEngine = ruleEngine;
        _contextProviders = contextProviders;
        _isMetricsEnabled = options.Value.IsMetricsEnabled;
        if (options.Value.IdentityKeys.Any())
            _identityKeys = options.Value.IdentityKeys.ToList();
        _client = httpClientFactory.CreateClient("ToggleMesh");
        
        var safeKey = GetSafeFileName(options.Value.ApiKey);

        if (options.Value.UseFallbackFile)
            _fallbackFilePath = string.IsNullOrWhiteSpace(options.Value.FallbackFilePath)
                ? Path.Combine(AppContext.BaseDirectory, ".togglemesh", $"{safeKey}.json")
                : options.Value.FallbackFilePath;

        var hubUrl = new Uri(
            new Uri(options.Value.EndpointUrl), Constants.Endpoints.ToggleHub);
        
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, connectionOptions =>
            {
                connectionOptions.Headers.Add("x-api-key", options.Value.ApiKey);
            })
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)
            ])
            .Build();

        _connection.On<FeatureFlagDto>("FlagUpdated", (flag) =>
        {
            _logger.LogDebug("[ToggleMesh] Flag updated remotely: {Key} (IsEnabled: {Value}, Rules: {RuleCount})", 
                flag.Key, flag.IsEnabled, flag.Rules.Count());
            
            _cache[flag.Key] = flag;
            _ = SaveFallbackAsync();
        });

        _connection.Reconnected += async _ =>
        {
            _logger.LogInformation("[ToggleMesh] SignalR reconnected. Syncing state.");
            await SyncStateWithApiAsync(CancellationToken.None);
        };

        _connection.Closed += async error =>
        {
            _logger.LogWarning(error, "[ToggleMesh] Connection closed permanently. Restarting background connection loop.");
            await EnsureConnectedLoopAsync(CancellationToken.None);
        };
    }
    
    public bool IsEnabled(string flagKey, bool defaultValue = false) => 
        IsEnabled(flagKey, string.Empty, (IDictionary<string, string>)new Dictionary<string, string>(), defaultValue);

    public bool IsEnabled(string flagKey, string identity, bool defaultValue = false) => 
        IsEnabled(flagKey, identity, (IDictionary<string, string>)new Dictionary<string, string>(), defaultValue);

    public bool IsEnabled(string flagKey, IDictionary<string, string> context, bool defaultValue = false) => 
        IsEnabled(flagKey, string.Empty, context, defaultValue);
    
    public bool IsEnabled<TContext>(string flagKey, TContext contextObject, bool defaultValue = false) =>
        IsEnabled(flagKey, string.Empty, ContextMapper<TContext>.ToDictionary(contextObject), defaultValue);

    public bool IsEnabled<TContext>(string flagKey, string identity, TContext contextObject, bool defaultValue = false) =>
        IsEnabled(flagKey, identity, ContextMapper<TContext>.ToDictionary(contextObject), defaultValue);

    public bool IsEnabled(string flagKey, string identity, IDictionary<string, string> context, bool defaultValue = false)
    {
        if (!_cache.TryGetValue(flagKey, out var flag))
            return defaultValue;

        bool result;

        if (!flag.IsEnabled || 
            !_ruleEngine.Evaluate(flag.Rules, GetMergedContext(context)))
            result = false;
        else
        {
            var actualIdentity = GetIdentity(identity, GetMergedContext(context));
            result = RolloutEvaluator.Evaluate(flag.RolloutPercentage, flagKey, actualIdentity);
        }
        
        UpdateMetrics(flagKey, result);
        return result;
    }
    
    private IDictionary<string, string> GetMergedContext(IDictionary<string, string> context)
    {
        var mergedContext = new Dictionary<string, string>(context, StringComparer.OrdinalIgnoreCase);
        foreach (var provider in _contextProviders)
        {
            var providerContext = provider.GetContext();
            foreach (var kvp in providerContext)
            {
                mergedContext.TryAdd(kvp.Key, kvp.Value);
            }
        }
        
        return mergedContext;
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
            try
            {
                var oldBuffer = Interlocked.Exchange(ref _metricsBuffer, new ConcurrentDictionary<string, FlagMetrics>());
                if (oldBuffer.IsEmpty)
                    continue;

                var payload = oldBuffer.Select(kvp => new
                {
                    kvp.Key,
                    TrueCount = Interlocked.Read(ref kvp.Value.TrueCount),
                    FalseCount = Interlocked.Read(ref kvp.Value.FalseCount)
                }).ToList();

                var response = await _client.PostAsJsonAsync(Constants.Endpoints.Metrics, payload, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[ToggleMesh] Failed to flush metrics. Status: {StatusCode}", response.StatusCode);
                    foreach (var kvp in oldBuffer)
                    {
                        var activeMetrics = _metricsBuffer.GetOrAdd(kvp.Key, _ => new FlagMetrics());
                        Interlocked.Add(ref activeMetrics.TrueCount, Interlocked.Read(ref kvp.Value.TrueCount));
                        Interlocked.Add(ref activeMetrics.FalseCount, Interlocked.Read(ref kvp.Value.FalseCount));
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "[ToggleMesh] Error during metrics flush.");
            }
        }
    }

    private string GetIdentity(string explicitIdentity, IDictionary<string, string> context)
    {
        if (!string.IsNullOrWhiteSpace(explicitIdentity)) return explicitIdentity;
        
        foreach (var key in _identityKeys)
        {
            if (context.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
            
            var caseInsensitiveKey = context.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (caseInsensitiveKey != null && !string.IsNullOrWhiteSpace(context[caseInsensitiveKey]))
            {
                return context[caseInsensitiveKey];
            }
        }
        
        return string.Empty;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ToggleMesh] SDK starting.");
        
        await LoadFallbackAsync();
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        
        try
        {
            await SyncStateWithApiAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[ToggleMesh] Initial sync timed out. Operating with offline cache/defaults.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ToggleMesh] Initial sync failed. Operating with offline cache/defaults.");
        }
        
        _ = Task.Run(() => EnsureConnectedLoopAsync(CancellationToken.None), CancellationToken.None);
        if (_isMetricsEnabled)
            _ = Task.Run(() => RunMetricsFlusherAsync(CancellationToken.None), CancellationToken.None);
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
                    await SyncStateWithApiAsync(ct);
                }
                break;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "[ToggleMesh] Background connection attempt failed. Retrying in 5s.");
                await Task.Delay(5000, ct);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ToggleMesh] SDK stopping.");
        await _connection.StopAsync(cancellationToken);
    }
    
    private async Task SyncStateWithApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            var flags = await _client.GetFromJsonAsync<List<FeatureFlagDto>>(
                Constants.Endpoints.GetAll, cancellationToken);

            if (flags is null)
            {
                _logger.LogWarning("[ToggleMesh] API returned null flag data.");
                return;
            }

            var fetchedKeys = flags.Select(f => f.Key).ToHashSet();
            var keysToRemove = _cache.Keys.Where(k => !fetchedKeys.Contains(k)).ToList();

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            foreach (var flag in flags)
            {
                _cache[flag.Key] = flag;
            }

            _logger.LogInformation("[ToggleMesh] State synchronized with API. Loaded {Count} flags.", flags.Count);
            
            await SaveFallbackAsync();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Failed to synchronize state with API.");
            throw;
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
                {
                    _cache[flag.Key] = flag;
                }
                _logger.LogInformation("[ToggleMesh] Loaded {Count} flags from offline fallback file.", flags.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Failed to read fallback file at {Path}", _fallbackFilePath);
        }
    }

    private async Task SaveFallbackAsync()
    {
        try
        {
            if (_fallbackFilePath is null)
            {
                _logger.LogTrace("[ToggleMesh] Fallback file path not configured. Skipping offline fallback save.");
                return;
            }
            
            var dir = Path.GetDirectoryName(_fallbackFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var content = JsonSerializer.Serialize(_cache.Values);
            await File.WriteAllTextAsync(_fallbackFilePath, content);
            _logger.LogTrace("[ToggleMesh] Fallback state saved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Failed to write fallback file to {Path}. Offline capabilities will be limited.", _fallbackFilePath);
        }
    }
    
    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed record FeatureFlagDto(string Key, bool IsEnabled, IEnumerable<RuleDto> Rules, int? RolloutPercentage = null);
}