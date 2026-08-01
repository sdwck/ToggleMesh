using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Infrastructure.Data;
using Quartz;

namespace ToggleMesh.API.Infrastructure.BackgroundServices;

[DisallowConcurrentExecution]
public class PendingChangesCleanupWorker : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PendingChangesCleanupWorker> _logger;

    public PendingChangesCleanupWorker(IServiceProvider serviceProvider, ILogger<PendingChangesCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("PendingChangesCleanupWorker executing job {JobKey}", context.JobDetail.Key);

        try
        {
            await ProcessPendingChangesAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing pending flag changes cleanup.");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }

    private async Task ProcessPendingChangesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var expiredChanges = await db.PendingFlagChanges
            .Where(x => x.Status == PendingFlagChangeStatus.PendingReview &&
                        x.ExecuteAt.HasValue && x.ExecuteAt.Value <= now)
            .ToListAsync(ct);

        foreach (var change in expiredChanges)
        {
            change.Status = PendingFlagChangeStatus.Expired;
            _logger.LogInformation("Flag change {ChangeId} expired before approval.", change.Id);
        }

        if (expiredChanges.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
