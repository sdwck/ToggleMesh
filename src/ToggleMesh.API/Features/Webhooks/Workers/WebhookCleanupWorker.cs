using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using Quartz;

namespace ToggleMesh.API.Features.Webhooks.Workers;

[DisallowConcurrentExecution]
public class WebhookCleanupWorker : IJob
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

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("WebhookCleanupWorker executing job {JobKey}", context.JobDetail.Key);

        try
        {
            await CleanupDeliveriesAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during webhook cleanup.");
            throw new JobExecutionException(ex, refireImmediately: false);
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
