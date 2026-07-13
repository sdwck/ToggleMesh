using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Analytics;

public class AnalyticsWorkerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IAnalyticsStorageSink> _mockSink = new();

    public AnalyticsWorkerTests(TestWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Analytics:Kafka:BootstrapServers"] = string.Empty,
                    ["Analytics:ClickHouse:ConnectionString"] = string.Empty
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_mockSink.Object);
            });
        });
    }

    [Fact]
    public async Task AnalyticsWorker_ShouldProcessQueueAndSaveToSink()
    {
        // Arrange
        var queue = _factory.Services.GetRequiredService<IAnalyticsEventPublisher>();
        var environmentId = Guid.NewGuid();

        var payload = new List<RawAnalyticsEventDto>();
        for (var i = 0; i < 50; i++)
        {
            payload.Add(new RawAnalyticsEventDto
            {
                Type = AnalyticsEventType.Exposure,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Identity = $"user-{i}",
                FlagKey = "test-flag",
                VariationId = Guid.NewGuid()
            });
        }

        var tcs = new TaskCompletionSource<bool>();
        var processedCount = 0;

        _mockSink.Setup(s => s.WriteBatchAsync(It.IsAny<List<AnalyticsExposure>>(), It.IsAny<List<AnalyticsTrack>>(), It.IsAny<CancellationToken>()))
            .Callback<List<AnalyticsExposure>, List<AnalyticsTrack>, CancellationToken>((exposures, _, _) =>
            {
                processedCount += exposures.Count;
                if (processedCount >= 50)
                {
                    tcs.TrySetResult(true);
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await queue.PublishBatchAsync(environmentId, payload);

        // Assert
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        
        completedTask.Should().Be(tcs.Task, "AnalyticsWorker should have processed the queued items and passed them to the Sink.");
        processedCount.Should().Be(50);
    }
}
