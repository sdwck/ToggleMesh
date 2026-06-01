using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Metrics;
using ToggleMesh.API.Persistence;

namespace ToggleMesh.API.BackgroundServices;

public class MetricsWorker : BackgroundService
{
    private readonly Channel<MetricQueueItem> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsWorker> _logger;

    public MetricsWorker(Channel<MetricQueueItem> channel, IServiceProvider serviceProvider, ILogger<MetricsWorker> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new Dictionary<(Guid, string), (long TrueCount, long FalseCount)>();
        var lastFlush = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            var timeToWait = TimeSpan.FromSeconds(5) - (DateTime.UtcNow - lastFlush);

            if (timeToWait <= TimeSpan.Zero || batch.Count >= 100)
            {
                if (batch.Count > 0 && await FlushToDatabaseAsync(batch, stoppingToken))
                    batch.Clear();
                lastFlush = DateTime.UtcNow;
                continue;
            }

            var readTask = _channel.Reader.WaitToReadAsync(stoppingToken).AsTask();
            var delayTask = Task.Delay(timeToWait, stoppingToken);
            var completedTask = await Task.WhenAny(readTask, delayTask);

            if (completedTask == delayTask)
                continue;

            if (!await readTask) 
                continue;
            
            while (batch.Count < 100 && _channel.Reader.TryRead(out var item))
            {
                var dictKey = (item.EnvironmentId, item.Key);
                batch.TryGetValue(dictKey, out var current);
                batch[dictKey] = (current.TrueCount + item.TrueCount, current.FalseCount + item.FalseCount);
            }
        }
    }

    private async Task<bool> FlushToDatabaseAsync(Dictionary<(Guid, string), (long True, long False)> batch, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            foreach (var kvp in batch)
            {
                var envId = kvp.Key.Item1;
                var flagKey = kvp.Key.Item2;
                var trueToAdd = kvp.Value.True;
                var falseToAdd = kvp.Value.False;

                await db.FeatureFlags
                    .Where(f => f.EnvironmentId == envId && f.Key == flagKey)
                    .ExecuteUpdateAsync(f => f
                        .SetProperty(p => p.TrueCount, p => p.TrueCount + trueToAdd)
                        .SetProperty(p => p.FalseCount, p => p.FalseCount + falseToAdd), ct);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush metrics to database.");
            return false;
        }
    }
}