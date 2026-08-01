using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using Quartz;

namespace ToggleMesh.API.Infrastructure.Security.Authorization.Workers;

[DisallowConcurrentExecution]
public class RefreshTokenCleanupService : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenCleanupService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<RefreshTokenCleanupService> logger,
        TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("RefreshTokenCleanupService executing job {JobKey}", context.JobDetail.Key);

        try
        {
            await DoCleanupAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during refresh token cleanup.");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }

    private async Task DoCleanupAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running expired and revoked refresh tokens cleanup...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var deletedCount = await dbContext.RefreshTokens
                .Where(t => t.Expires <= now || t.Revoked != null)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation("Successfully cleaned up {DeletedCount} expired or revoked refresh tokens.", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during refresh token cleanup.");
        }
    }
}
