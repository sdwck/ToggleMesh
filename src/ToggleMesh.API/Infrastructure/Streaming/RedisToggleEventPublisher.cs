using System.Text.Json;
using StackExchange.Redis;

namespace ToggleMesh.API.Infrastructure.Streaming;

public class RedisToggleEventPublisher : IToggleEventPublisher
{
    private readonly IConnectionMultiplexer _redis;
    public const string ChannelName = "togglemesh-sse-updates";
    public static readonly RedisChannel ChannelNameLiteral = RedisChannel.Literal("togglemesh-sse-updates");

    public RedisToggleEventPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishEventAsync<T>(string environmentId, string eventName, T payload)
    {
        var wrapper = new 
        { 
            EventName = eventName,
            EnvironmentId = environmentId, 
            Payload = payload 
        };
        var json = JsonSerializer.Serialize(wrapper);
        await _redis.GetSubscriber()
            .PublishAsync(ChannelNameLiteral, json);
    }
}
