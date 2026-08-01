using System.Diagnostics;
using System.Threading.Channels;
using Quartz;
using StackExchange.Redis;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Streaming;

namespace ToggleMesh.API.Features.Analytics.Ingest;

[DisallowConcurrentExecution]
public class RollupWorker : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RollupWorker> _logger;

    public RollupWorker(IServiceProvider serviceProvider, ILogger<RollupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var stoppingToken = context.CancellationToken;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var queryEngine = scope.ServiceProvider.GetRequiredService<IAnalyticsQueryEngine>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var math = scope.ServiceProvider.GetRequiredService<BayesianMathService>();
            var mabShifter = scope.ServiceProvider.GetRequiredService<IMabTrafficShifterService>();
            scope.ServiceProvider.GetRequiredService<Channel<WebhookEvent>>();

            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[RollupWorker] Running aggregation pipeline...");
            await queryEngine.AggregateMetricsAsync(stoppingToken);
            await queryEngine.AggregateContextualMetricsAsync(stoppingToken);
            sw.Stop();
            _logger.LogInformation("[RollupWorker] Aggregation pipeline completed in {ElapsedMilliseconds}ms.", sw.ElapsedMilliseconds);

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
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RollupWorker] Error during aggregation.");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
