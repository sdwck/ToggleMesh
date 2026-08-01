using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Features.Flags.ScheduledChanges;
using ToggleMesh.API.Features.Flags.Commands;
using FastEndpoints;

namespace ToggleMesh.API.Infrastructure.BackgroundServices;

public class ScheduledChangesWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledChangesWorker> _logger;

    public ScheduledChangesWorker(IServiceProvider serviceProvider, ILogger<ScheduledChangesWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledChangesWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAndScheduledChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing scheduled flag changes.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessPendingAndScheduledChangesAsync(CancellationToken ct)
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

        var dueScheduledChangesIds = await db.PendingFlagChanges
            .Where(x => x.Status == PendingFlagChangeStatus.Scheduled &&
                        (!x.ExecuteAt.HasValue || x.ExecuteAt.Value <= now))
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (expiredChanges.Count > 0)
            await db.SaveChangesAsync(ct);

        foreach (var changeId in dueScheduledChangesIds)
        {
            using var innerScope = _serviceProvider.CreateScope();
            var innerDb = innerScope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var change = await innerDb.PendingFlagChanges.FindAsync([changeId], ct);
            if (change == null) continue;
            if (change.Status != PendingFlagChangeStatus.Scheduled) 
                continue;

            innerDb.SystemActorEmail = $"System (Scheduled Execution - Author {change.RequestedByUserId})";

            try
            {
                var state = await FlagPatchApplicationHelper.ApplyPatchAsync(innerDb, change, ct);
                _logger.LogInformation("Successfully executed scheduled flag change {ChangeId}.", change.Id);

                try
                {
                    await new NotifyFlagUpdatedCommand(
                        change.EnvironmentId, 
                        state.FeatureFlag.Key, 
                        state.ToDto(),
                        state.ToSdkDto()
                    ).ExecuteAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish flag update notification for executed change {ChangeId}.", change.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply patch for change {ChangeId}, marking as ConflictFailed.", changeId);
                
                using var failScope = _serviceProvider.CreateScope();
                var failDb = failScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var failChange = await failDb.PendingFlagChanges.FindAsync([changeId], ct);
                if (failChange != null)
                {
                    failChange.Status = PendingFlagChangeStatus.ConflictFailed;
                    await failDb.SaveChangesAsync(ct);
                }
            }
        }
    }
}
