using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Streaming;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class RollupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RollupWorker> _logger;
    private readonly TimeSpan _rollupInterval;

    public RollupWorker(IServiceProvider serviceProvider, ILogger<RollupWorker> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _rollupInterval = configuration.GetValue("Analytics:RollupInterval", TimeSpan.FromMinutes(15));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[RollupWorker] Starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var queryEngine = scope.ServiceProvider.GetRequiredService<IAnalyticsQueryEngine>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var math = scope.ServiceProvider.GetRequiredService<BayesianMathService>();
                var mabShifter = scope.ServiceProvider.GetRequiredService<IMabTrafficShifterService>();
                scope.ServiceProvider.GetRequiredService<Channel<WebhookEvent>>();

                _logger.LogInformation("[RollupWorker] Running aggregation pipeline...");
                await queryEngine.AggregateMetricsAsync(stoppingToken);
                await queryEngine.AggregateContextualMetricsAsync(stoppingToken);
                _logger.LogInformation("[RollupWorker] Aggregation pipeline completed.");

                var notifyHandler = new NotifyFlagUpdatedCommandHandler(
                    scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>(),
                    scope.ServiceProvider.GetRequiredService<ICacheInvalidator>(),
                    scope.ServiceProvider.GetRequiredService<IToggleEventPublisher>(),
                    scope.ServiceProvider.GetRequiredService<ILogger<NotifyFlagUpdatedCommandHandler>>(),
                    scope.ServiceProvider.GetRequiredService<IConfiguration>()
                );

                await mabShifter.ProcessMabTrafficShiftingAsync(db, math, notifyHandler, stoppingToken);
                await mabShifter.ProcessContextualBanditAutoSegmentationAsync(db, math, notifyHandler, stoppingToken);
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
                _logger.LogError(ex, "[RollupWorker] Error during aggregation.");
            }

            try
            {
                await Task.Delay(_rollupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
