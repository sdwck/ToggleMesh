using System.Text.Json;
using StackExchange.Redis;

namespace ToggleMesh.API.Infrastructure.Streaming;

public class SseRedisSubscriber : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ISseConnectionManager _connectionManager;
    private readonly ILogger<SseRedisSubscriber> _logger;

    public SseRedisSubscriber(
        IConnectionMultiplexer redis,
        ISseConnectionManager connectionManager,
        ILogger<SseRedisSubscriber> logger)
    {
        _redis = redis;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();
        
        await subscriber.SubscribeAsync(RedisToggleEventPublisher.ChannelNameLiteral, async void (_, message) =>
        {
            try
            {
                if (message.IsNullOrEmpty) 
                    return;
                
                var payloadStr = message.ToString();
                var doc = JsonDocument.Parse(payloadStr);
                
                if (doc.RootElement.TryGetProperty("EnvironmentId", out var envIdElement))
                {
                    var environmentId = envIdElement.GetString();
                    if (!string.IsNullOrEmpty(environmentId))
                    {
                        await _connectionManager.BroadcastToEnvironmentAsync(environmentId, payloadStr);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SSE Redis message");
            }
        });

        _logger.LogInformation("SseRedisSubscriber started and subscribed to {Channel}", RedisToggleEventPublisher.ChannelName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
        }

        await subscriber.UnsubscribeAsync(RedisToggleEventPublisher.ChannelNameLiteral);
    }
}
