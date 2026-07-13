using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.Kafka;
using ToggleMesh.API.Features.Analytics.Ingest;

namespace ToggleMesh.IntegrationTests.Analytics;

public class KafkaPublisherTests : IAsyncLifetime
{
    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder(
            "confluentinc/cp-kafka:7.4.0")
        .Build();

    public async Task InitializeAsync()
    {
        await _kafkaContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _kafkaContainer.DisposeAsync();
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldDeliverMessageToKafka()
    {
        // Arrange
        var topic = "test-topic-" + Guid.NewGuid().ToString("N");

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Analytics:Kafka:BootstrapServers", _kafkaContainer.GetBootstrapAddress() },
            { "Analytics:Kafka:Topic", topic }
        });
        var configuration = configBuilder.Build();
        
        var environmentId = Guid.NewGuid();
        var events = new List<RawAnalyticsEventDto>
        {
            new()
            {
                Type = AnalyticsEventType.Exposure, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Identity = "user-1", FlagKey = "flag-1", VariationId = Guid.NewGuid()
            }
        };

        // Act
        using (var publisher = new KafkaAnalyticsPublisher(configuration, NullLogger<KafkaAnalyticsPublisher>.Instance))
        {
            await publisher.PublishBatchAsync(environmentId, events);
        }

        // Assert
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _kafkaContainer.GetBootstrapAddress(),
            GroupId = "test-group-" + Guid.NewGuid().ToString("N"),
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(topic);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var consumeResult = consumer.Consume(cts.Token);

        consumeResult.Should().NotBeNull();
        consumeResult.Message.Key.Should().Be(environmentId.ToString());

        var payload = JsonSerializer.Deserialize<KafkaMessagePayload>(consumeResult.Message.Value);
        payload.Should().NotBeNull();
        payload.EnvironmentId.Should().Be(environmentId);
        payload.Events.Should().HaveCount(1);
        payload.Events[0].Identity.Should().Be("user-1");
    }
}
