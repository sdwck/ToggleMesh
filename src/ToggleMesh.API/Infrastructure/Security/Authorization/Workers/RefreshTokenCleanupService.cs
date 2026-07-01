using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Auth.Workers;

public class RefreshTokenCleanupService : BackgroundService
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = _configuration.GetValue("Auth:RefreshTokenCleanupIntervalHours", 24);
        _logger.LogInformation("RefreshTokenCleanupService started. Cleanup interval: {IntervalHours} hours.", intervalHours);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours), _timeProvider);

        try
        {
            await DoCleanupAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
                await DoCleanupAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RefreshTokenCleanupService is shutting down.");
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
