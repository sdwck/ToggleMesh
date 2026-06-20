using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Webhooks;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.BackgroundServices.Webhooks;

public class WebhookCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookCleanupWorker> _logger;

    public WebhookCleanupWorker(IServiceProvider serviceProvider, ILogger<WebhookCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupDeliveriesAsync(stoppingToken);
            }
            catch (Exception ex)
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

        var successThreshold = DateTime.UtcNow.AddDays(-7);
        var failedThreshold = DateTime.UtcNow.AddDays(-30);

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
