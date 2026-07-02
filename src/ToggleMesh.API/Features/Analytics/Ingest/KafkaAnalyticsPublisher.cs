using System.Text.Json;
using Confluent.Kafka;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class KafkaAnalyticsPublisher : IAnalyticsEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaAnalyticsPublisher> _logger;

    public KafkaAnalyticsPublisher(IConfiguration configuration, ILogger<KafkaAnalyticsPublisher> logger)
    {
        _logger = logger;
        _topic = configuration["Analytics:Kafka:Topic"] ?? "togglemesh-events";
        var bootstrapServers = configuration["Analytics:Kafka:BootstrapServers"] ?? "localhost:9092";

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.Leader,
            LingerMs = 50
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
        _logger.LogInformation("Initialized KafkaAnalyticsPublisher for topic {Topic} at {Servers}", _topic, bootstrapServers);
    }

    public async ValueTask PublishBatchAsync(Guid environmentId, List<RawAnalyticsEventDto> events, CancellationToken ct = default)
    {
        if (events == null || events.Count == 0) return;

        var payload = new KafkaMessagePayload
        {
            EnvironmentId = environmentId,
            Events = events
        };

        var message = new Message<string, string>
        {
            Key = environmentId.ToString(),
            Value = JsonSerializer.Serialize(payload)
        };

        try
        {
            var deliveryResult = await _producer.ProduceAsync(_topic, message, ct);
            _logger.LogTrace("Delivered message to {TopicPartitionOffset}", deliveryResult.TopicPartitionOffset);
        }
        catch (ProduceException<string, string> e)
        {
            _logger.LogError(e, "Delivery failed: {ErrorReason}", e.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        try 
        {
            _producer?.Flush(TimeSpan.FromSeconds(2));
        }
        catch { /* ignore */ }
        
        _producer?.Dispose();
    }
}
