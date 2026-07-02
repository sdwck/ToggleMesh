using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Features.Webhooks.Workers;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Workers;

[Collection("SharedEnv4")]
public class WebhookCleanupWorkerTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private AppDbContext _db = null!;
    private WebhookCleanupWorker _worker = null!;

    public WebhookCleanupWorkerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        var scope = _factory.Services.CreateScope();
        _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebhookCleanupWorker>>();
        _worker = new WebhookCleanupWorker(_factory.Services, logger, _factory.TimeProvider);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Worker_ShouldDeleteSuccessfulWebhooksOlderThan7Days()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var project = new Project { Id = projectId, Name = "Test Project" };
        await _db.Projects.AddAsync(project);

        var webhookId = Guid.CreateVersion7();
        await _db.Webhooks.AddAsync(new Webhook
        {
            Id = webhookId,
            ProjectId = projectId,
            Url = "https://example.com"
        });

        var now = _factory.TimeProvider.GetUtcNow().UtcDateTime;

        var recentSuccess = new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            WebhookId = webhookId,
            Status = WebhookDeliveryStatus.Success,
            CreatedAt = now.AddDays(-6),
            Payload = "{}"
        };

        var oldSuccess = new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            WebhookId = webhookId,
            Status = WebhookDeliveryStatus.Success,
            CreatedAt = now.AddDays(-8),
            Payload = "{}"
        };

        await _db.WebhookDeliveries.AddRangeAsync(recentSuccess, oldSuccess);
        await _db.SaveChangesAsync();

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var method = typeof(WebhookCleanupWorker).GetMethod("CleanupDeliveriesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(_worker, [cts.Token])!;

        // Assert
        var deliveries = await _db.WebhookDeliveries.ToListAsync(cancellationToken: cts.Token);
        deliveries.Should().ContainSingle();
        deliveries.First().Id.Should().Be(recentSuccess.Id);
    }

    [Fact]
    public async Task Worker_ShouldDeleteFailedWebhooksOlderThan30Days()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var project = new Project { Id = projectId, Name = "Test Project" };
        await _db.Projects.AddAsync(project);

        var webhookId = Guid.CreateVersion7();
        await _db.Webhooks.AddAsync(new Webhook
        {
            Id = webhookId,
            ProjectId = projectId,
            Url = "https://example.com"
        });

        var now = _factory.TimeProvider.GetUtcNow().UtcDateTime;

        var recentFailed = new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            WebhookId = webhookId,
            Status = WebhookDeliveryStatus.Failed,
            CreatedAt = now.AddDays(-29),
            Payload = "{}"
        };

        var oldFailed = new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            WebhookId = webhookId,
            Status = WebhookDeliveryStatus.Failed,
            CreatedAt = now.AddDays(-31),
            Payload = "{}"
        };

        await _db.WebhookDeliveries.AddRangeAsync(recentFailed, oldFailed);
        await _db.SaveChangesAsync();

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var method = typeof(WebhookCleanupWorker).GetMethod("CleanupDeliveriesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(_worker, [cts.Token])!;

        // Assert
        var deliveries = await _db.WebhookDeliveries.ToListAsync(cancellationToken: cts.Token);
        deliveries.Should().ContainSingle();
        deliveries.First().Id.Should().Be(recentFailed.Id);
    }
}
