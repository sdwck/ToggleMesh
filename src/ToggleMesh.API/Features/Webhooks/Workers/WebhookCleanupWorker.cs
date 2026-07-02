using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Webhooks.Workers;

public class WebhookCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookCleanupWorker> _logger;
    private readonly TimeProvider _timeProvider;

    public WebhookCleanupWorker(IServiceProvider serviceProvider, ILogger<WebhookCleanupWorker> logger, TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupDeliveriesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during webhook cleanup.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupDeliveriesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var successThreshold = now.AddDays(-7);
        var failedThreshold = now.AddDays(-30);

        var deletedSuccess = await db.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Success && d.CreatedAt < successThreshold)
            .ExecuteDeleteAsync(ct);

        var deletedFailed = await db.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Failed && d.CreatedAt < failedThreshold)
            .ExecuteDeleteAsync(ct);

        if (deletedSuccess > 0 || deletedFailed > 0)
            _logger.LogInformation("Cleaned up {SuccessCount} old successful and {FailedCount} old failed webhook deliveries.", deletedSuccess, deletedFailed);
    }
}
