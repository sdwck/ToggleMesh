using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToggleMesh.API.Infrastructure.BackgroundServices.Email;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Email.Models;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Workers;

[Collection("SharedEnv4")]
public class EmailOutboxWorkerTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private AppDbContext _db = null!;
    private EmailOutboxWorker _worker = null!;

    public EmailOutboxWorkerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        var scope = _factory.Services.CreateScope();
        _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EmailOutboxWorker>>();
        _worker = new EmailOutboxWorker(_factory.Services, logger, _factory.TimeProvider);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Worker_ShouldSendPendingEmail_AndMarkAsSent()
    {
        // Arrange
        var now = _factory.TimeProvider.GetUtcNow().UtcDateTime;
        var message = new EmailOutboxMessage
        {
            Id = Guid.CreateVersion7(),
            ToEmail = "test@example.com",
            Subject = "Test Subject",
            HtmlBody = "<h1>Test</h1>",
            Status = EmailOutboxStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
            AttemptCount = 0
        };

        await _db.EmailOutboxMessages.AddAsync(message);
        await _db.SaveChangesAsync();

        // Act
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var method = typeof(EmailOutboxWorker).GetMethod("ProcessOutboxAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(_worker, [cts.Token])!;

        // Assert
        await _db.Entry(message).ReloadAsync(cts.Token);
        var updatedMessage = message;
        updatedMessage.Should().NotBeNull();
        updatedMessage.Status.Should().Be(EmailOutboxStatus.Sent);
        updatedMessage.AttemptCount.Should().Be(1);
        updatedMessage.CompletedAt.Should().Be(now);
    }
}
