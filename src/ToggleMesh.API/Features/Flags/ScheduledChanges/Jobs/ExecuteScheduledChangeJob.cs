using Quartz;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Flags.ScheduledChanges;
using FastEndpoints;

namespace ToggleMesh.API.Features.Flags.ScheduledChanges.Jobs;

public class ExecuteScheduledChangeJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExecuteScheduledChangeJob> _logger;

    public ExecuteScheduledChangeJob(IServiceProvider serviceProvider, ILogger<ExecuteScheduledChangeJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var changeIdStr = context.JobDetail.JobDataMap.GetString("ChangeId");
        if (!Guid.TryParse(changeIdStr, out var changeId))
        {
            _logger.LogError("ExecuteScheduledChangeJob failed: Invalid or missing ChangeId in JobDataMap");
            return;
        }

        _logger.LogInformation("ExecuteScheduledChangeJob executing for change {ChangeId}", changeId);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var change = await db.PendingFlagChanges.FindAsync([changeId], context.CancellationToken);
        if (change == null)
        {
            _logger.LogWarning("ExecuteScheduledChangeJob skipped: Change {ChangeId} not found", changeId);
            return;
        }

        if (change.Status != PendingFlagChangeStatus.Scheduled)
        {
            _logger.LogInformation("ExecuteScheduledChangeJob skipped: Change {ChangeId} is not in Scheduled status (Status: {Status})", changeId, change.Status);
            return;
        }

        db.SystemActorEmail = $"System (Scheduled Execution - Author {change.RequestedByUserId})";

        try
        {
            var state = await FlagPatchApplicationHelper.ApplyPatchAsync(db, change, context.CancellationToken);
            _logger.LogInformation("Successfully executed scheduled flag change {ChangeId}.", change.Id);

            try
            {
                await new NotifyFlagUpdatedCommand(
                    change.EnvironmentId, 
                    state.FeatureFlag.Key, 
                    state.ToDto(),
                    state.ToSdkDto()
                ).ExecuteAsync(context.CancellationToken);
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
            var failChange = await failDb.PendingFlagChanges.FindAsync([changeId], context.CancellationToken);
            if (failChange != null)
            {
                failChange.Status = PendingFlagChangeStatus.ConflictFailed;
                await failDb.SaveChangesAsync(context.CancellationToken);
            }
        }
    }
}
