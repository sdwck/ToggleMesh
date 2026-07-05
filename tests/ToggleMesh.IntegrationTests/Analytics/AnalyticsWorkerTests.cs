using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;
using Xunit;

namespace ToggleMesh.IntegrationTests.Analytics;

public class AnalyticsWorkerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

    public AnalyticsWorkerTests(TestWebApplicationFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Analytics:Kafka:BootstrapServers", "");
            builder.UseSetting("Analytics:ClickHouse:ConnectionString", "");
        });
    }

    [Fact]
    public async Task AnalyticsWorker_ShouldProcessQueueAndSaveToDatabase()
    {
        // Arrange
        var queue = _factory.Services.GetRequiredService<IAnalyticsEventPublisher>();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var environmentId = Guid.NewGuid();

        for (int i = 0; i < 50; i++)
        {
            var payload = new List<RawAnalyticsEventDto>
            {
                new()
                {
                    Type = AnalyticsEventType.Exposure,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Identity = $"user-{i}",
                    FlagKey = "test-flag",
                    Result = true
                }
            };
            await queue.PublishBatchAsync(environmentId, payload);
        }

        // Act & Assert
        bool found = false;
        for (int i = 0; i < 20; i++)
        {
            var count = await db.AnalyticsExposures.CountAsync(e => e.EnvironmentId == environmentId);
            if (count == 50)
            {
                found = true;
                break;
            }
            await Task.Delay(500);
        }

        found.Should().BeTrue("AnalyticsWorker should have processed the queued items and saved them to the DB.");
    }
}
