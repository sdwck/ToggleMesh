using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Metrics.Domain;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Metrics.Workers;

public class MetricsWorker : BackgroundService
{
    private readonly Channel<MetricQueueItem> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsWorker> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _configuration;

    public MetricsWorker(
        Channel<MetricQueueItem> channel,
        IServiceProvider serviceProvider,
        ILogger<MetricsWorker> logger,
        TimeProvider timeProvider, 
        IConfiguration configuration)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _timeProvider = timeProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new Dictionary<(Guid, string), (long TrueCount, long FalseCount, bool IsClientSide)>();
        var lastFlush = _timeProvider.GetUtcNow();
        var maxMetricsString = _configuration.GetSection("Metrics").GetSection("MaxMetrics").Value;
        if (!int.TryParse(maxMetricsString, out var maxMetrics))
            maxMetrics = 10_000;

        while (!stoppingToken.IsCancellationRequested)
        {
            var timeToWait = TimeSpan.FromSeconds(5) - (_timeProvider.GetUtcNow() - lastFlush);

            if (timeToWait <= TimeSpan.Zero || batch.Count >= 100)
            {
                if (batch.Count > 0)
                {
                    if (await FlushToDatabaseAsync(batch, stoppingToken))
                        batch.Clear();
                    else if (batch.Count > maxMetrics)
                    {
                        _logger.LogWarning("[ToggleMesh] DB is unreachable. Dropping {Count} pending metrics.", batch.Count);
                        batch.Clear();
                    }
                    else
                        await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
                }
                lastFlush = _timeProvider.GetUtcNow();
                continue;
            }

            var readTask = _channel.Reader.WaitToReadAsync(stoppingToken).AsTask();
            var delayTask = Task.Delay(timeToWait, _timeProvider, stoppingToken);
            var completedTask = await Task.WhenAny(readTask, delayTask);

            if (completedTask == delayTask)
                continue;

            if (!await readTask)
                continue;

            while (batch.Count < 100 && _channel.Reader.TryRead(out var item))
            {
                var dictKey = (item.EnvironmentId, item.Key);
                if (batch.TryGetValue(dictKey, out var current))
                {
                    batch[dictKey] = (
                        current.TrueCount + item.TrueCount,
                        current.FalseCount + item.FalseCount,
                        current.IsClientSide && item.IsClientSideExposed
                    );
                }
                else
                {
                    batch[dictKey] = (item.TrueCount, item.FalseCount, item.IsClientSideExposed);
                }
            }
        }
    }

    private async Task<bool> FlushToDatabaseAsync(Dictionary<(Guid, string), (long True, long False, bool IsClient)> batch,
        CancellationToken ct)
    {
        var sortedBatch = batch
            .OrderBy(x => x.Key.Item1)
            .ThenBy(x => x.Key.Item2)
            .ToList();
        
        var count = batch.Count;
        var envIds = new Guid[count];
        var keys = new string[count];
        var isClientFlags = new bool[count];
        var trueCounts = new long[count];
        var falseCounts = new long[count];

        var index = 0;
        try
        {
            foreach (var kvp in sortedBatch)
            {
                envIds[index] = kvp.Key.Item1;
                keys[index] = kvp.Key.Item2;
                isClientFlags[index] = kvp.Value.IsClient;
                trueCounts[index] = kvp.Value.True;
                falseCounts[index] = kvp.Value.False;
                index++;
            }

            await BulkFlushMetricsAsync(envIds, keys, isClientFlags, trueCounts, falseCounts, ct);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush metrics to database.");
            return false;
        }
    }
    
    private async Task BulkFlushMetricsAsync(Guid[] envIds, string[] keys, bool[] isClientFlags, long[] trueCounts, long[] falseCounts, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await db.Database.ExecuteSqlAsync($@"
                INSERT INTO ""FlagMetricBuckets"" (""EnvironmentId"", ""FlagKey"", ""TimestampBucket"", ""TrueCount"", ""FalseCount"")
                SELECT 
                    upd.env_id,
                    upd.flag_key,
                    DATE_TRUNC('hour', now()),
                    upd.true_add,
                    upd.false_add
                FROM (
                    SELECT 
                        unnest({envIds}::uuid[]) AS env_id,
                        unnest({keys}::text[]) AS flag_key,
                        unnest({isClientFlags}::boolean[]) AS is_client,
                        unnest({trueCounts}::bigint[]) AS true_add,
                        unnest({falseCounts}::bigint[]) AS false_add
                ) AS upd
                JOIN ""FlagEnvironmentStates"" fes ON fes.""EnvironmentId"" = upd.env_id
                JOIN ""ProjectFeatureFlags"" ff ON ff.""Id"" = fes.""FeatureFlagId"" AND ff.""Key"" = upd.flag_key
                WHERE (upd.is_client = FALSE OR ff.""IsClientSideExposed"" = TRUE)
                ON CONFLICT (""EnvironmentId"", ""FlagKey"", ""TimestampBucket"")
                DO UPDATE SET 
                    ""TrueCount"" = ""FlagMetricBuckets"".""TrueCount"" + EXCLUDED.""TrueCount"",
                    ""FalseCount"" = ""FlagMetricBuckets"".""FalseCount"" + EXCLUDED.""FalseCount"";", ct);
    }
}