using System.Collections.Concurrent;
using System.Net.Http.Json;
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

    public ToggleMeshClient(
        IHttpClientFactory httpClientFactory,
        IOptions<ToggleMeshOptions> options, 
        ILogger<ToggleMeshClient> logger)
    {
        _logger = logger;
        _client = httpClientFactory.CreateClient("ToggleMesh");
        var hubUrl = new Uri(
            new Uri(options.Value.EndpointUrl), Constants.Endpoints.ToggleHub);
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, bool>("FlagUpdated", (key, value) =>
        {
            _cache[key] = value;
        });
    }
    
    public bool IsEnabled(string flagKey)
    {
        return _cache.TryGetValue(flagKey, out var isEnabled) && isEnabled;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("[ToggleMesh] SDK is starting...");
        await FetchInitialDataAsync(cancellationToken);
        await _connection.StartAsync(cancellationToken);
        _logger.LogInformation("[ToggleMesh] Connected to the API.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("[ToggleMesh] SDK is stopping...");
        await _connection.StopAsync(cancellationToken);
    }
    
    private async Task FetchInitialDataAsync(CancellationToken cancellationToken)
    {
        var flags = await _client.GetFromJsonAsync<List<FeatureFlagDto>>(
            Constants.Endpoints.GetAll, cancellationToken);

        if (flags is null)
        {
            _logger.LogError("[ToggleMesh] Failed to fetch initial feature flags.");
            return;
        }
        
        _logger.LogInformation("[ToggleMesh] Fetched {Count} feature flags.", flags.Count);
        _cache.Clear();

        foreach (var flag in flags)
        {
            _cache.TryAdd(flag.Key, flag.IsEnabled);
        }
    }
    
    private sealed record FeatureFlagDto(string Key, bool IsEnabled);
}