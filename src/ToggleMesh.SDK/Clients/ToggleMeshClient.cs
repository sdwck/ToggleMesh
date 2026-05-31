using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToggleMesh.SDK.Options;

namespace ToggleMesh.SDK.Clients;

public class ToggleMeshClient : IToggleMeshClient, IHostedService
{
    private readonly ConcurrentDictionary<string, bool> _cache = new();
    private readonly HubConnection _connection;
    private readonly ILogger<ToggleMeshClient> _logger;
    private readonly HttpClient _client;
    private readonly string _fallbackFilePath;

    public ToggleMeshClient(
        IHttpClientFactory httpClientFactory,
        IOptions<ToggleMeshOptions> options, 
        ILogger<ToggleMeshClient> logger)
    {
        _logger = logger;
        _client = httpClientFactory.CreateClient("ToggleMesh");
        
        _fallbackFilePath = string.IsNullOrWhiteSpace(options.Value.FallbackFilePath)
            ? Path.Combine(AppContext.BaseDirectory, ".togglemesh", $"{options.Value.ApiKey}.json")
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

        _connection.On<string, bool>("FlagUpdated", (key, value) =>
        {
            _logger.LogDebug("[ToggleMesh] Flag updated remotely: {Key} = {Value}", key, value);
            _cache[key] = value;
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
    
    public bool IsEnabled(string flagKey, bool defaultValue = false)
    {
        return _cache.GetValueOrDefault(flagKey, defaultValue);
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
    }

    private async Task EnsureConnectedLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_connection.State == HubConnectionState.Disconnected)
                {
                    _logger.LogTrace("[ToggleMesh] Attempting to establish SignalR connection.");
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
                _cache[flag.Key] = flag.IsEnabled;
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
            if (!File.Exists(_fallbackFilePath))
            {
                _logger.LogTrace("[ToggleMesh] No fallback file found at {Path}", _fallbackFilePath);
                return;
            }

            var content = await File.ReadAllTextAsync(_fallbackFilePath);
            var flags = JsonSerializer.Deserialize<Dictionary<string, bool>>(content);
            
            if (flags != null)
            {
                foreach (var (key, value) in flags)
                {
                    _cache[key] = value;
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
            var dir = Path.GetDirectoryName(_fallbackFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var content = JsonSerializer.Serialize(_cache);
            await File.WriteAllTextAsync(_fallbackFilePath, content);
            _logger.LogTrace("[ToggleMesh] Fallback state saved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ToggleMesh] Failed to write fallback file to {Path}. Offline capabilities will be limited.", _fallbackFilePath);
        }
    }
    
    private sealed record FeatureFlagDto(string Key, bool IsEnabled);
}