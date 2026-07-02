using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Infrastructure.Caching;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class AnalyticsWorker : BackgroundService
{
    private readonly InMemoryAnalyticsQueue _queue;
    private readonly IAnalyticsStorageSink _sink;
    private readonly ILogger<AnalyticsWorker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _memoryCache;

    public AnalyticsWorker(InMemoryAnalyticsQueue queue, IAnalyticsStorageSink sink, ILogger<AnalyticsWorker> logger, IConnectionMultiplexer redis, IMemoryCache memoryCache)
    {
        _queue = queue;
        _sink = sink;
        _logger = logger;
        _redis = redis;
        _memoryCache = memoryCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AnalyticsWorker] Started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var batch = new List<AnalyticsBatchMessage>();
            try
            {
                var firstMsg = await _queue.ReadAllAsync(stoppingToken).FirstOrDefaultAsync(stoppingToken);
                if (firstMsg != null)
                    batch.Add(firstMsg);

                using var batchCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, batchCts.Token);
                
                try
                {
                    await foreach (var msg in _queue.ReadAllAsync(linkedCts.Token))
                    {
                        batch.Add(msg);
                        if (batch.Count >= 1000) 
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                }

                if (batch.Count > 0)
                    // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                    await ProcessBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AnalyticsWorker] Error during batch processing.");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessBatchAsync(List<AnalyticsBatchMessage> batch, CancellationToken ct)
    {
        var exposures = new List<AnalyticsExposure>();
        var tracks = new List<AnalyticsTrack>();

        foreach (var msg in batch)
        {
            foreach (var evt in msg.Events)
            {
                if (evt.Type == AnalyticsEventType.Exposure)
                {
                    exposures.Add(new AnalyticsExposure
                    {
                        Id = Guid.CreateVersion7(),
                        EnvironmentId = msg.EnvironmentId,
                        Identity = evt.Identity,
                        FlagKey = evt.FlagKey ?? string.Empty,
                        Variant = evt.Result,
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(evt.Timestamp),
                        Properties = evt.Properties != null ? JsonSerializer.SerializeToDocument(evt.Properties) : null
                    });
                }
                else
                {
                    tracks.Add(new AnalyticsTrack
                    {
                        Id = Guid.CreateVersion7(),
                        EnvironmentId = msg.EnvironmentId,
                        Identity = evt.Identity,
                        EventName = evt.EventName ?? string.Empty,
                        Value = evt.Value,
                        Properties = evt.Properties != null ? JsonSerializer.SerializeToDocument(evt.Properties) : null,
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(evt.Timestamp)
                    });
                }
            }
        }

        await _sink.WriteBatchAsync(exposures, tracks, ct);

        try
        {
            await ProcessSchemaAsync(exposures, tracks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AnalyticsWorker] Error processing schema.");
        }
    }

    private async Task ProcessSchemaAsync(List<AnalyticsExposure> exposures, List<AnalyticsTrack> tracks)
    {
        var db = _redis.GetDatabase();

        var allEvents = tracks
            .Select(t => new { t.EnvironmentId, t.EventName })
            .Distinct()
            .ToList();

        foreach (var ev in allEvents)
        {
            await db.SetAddAsync(CacheKeys.UniqueEvents(ev.EnvironmentId), ev.EventName);
        }

        var eventsWithValue = tracks
            .Where(t => t.Value.HasValue)
            .Select(t => new { t.EnvironmentId, t.EventName })
            .Distinct()
            .ToList();

        foreach (var ev in eventsWithValue)
        {
            var cacheKey = CacheKeys.EventSchemaHasValue(ev.EnvironmentId, ev.EventName);
            
            if (!_memoryCache.TryGetValue(cacheKey, out _))
            {
                await db.StringSetAsync(cacheKey, "1", TimeSpan.FromDays(30));
                _memoryCache.Set(cacheKey, true, TimeSpan.FromMinutes(1));
            }
        }

        var flagsWithContext = exposures
            .Where(e => e.Properties != null)
            .GroupBy(e => new { e.EnvironmentId, e.FlagKey })
            .ToList();

        foreach (var group in flagsWithContext)
        {
            var keys = new HashSet<string>();
            foreach (var exposure in group)
            {
                if (exposure.Properties == null) continue;
                foreach (var prop in exposure.Properties.RootElement.EnumerateObject())
                {
                    keys.Add(prop.Name);
                }
            }

            if (keys.Count > 0)
            {
                var cacheKey = CacheKeys.FlagSchemaContextKeys(group.Key.EnvironmentId, group.Key.FlagKey);
                var memKey = $"{cacheKey}_hash";
                var keysHash = string.Join(",", keys.OrderBy(k => k));
                
                if (!_memoryCache.TryGetValue(memKey, out string? lastHash) || lastHash != keysHash)
                {
                    var redisKeys = keys.Select(k => (RedisValue)k).ToArray();
                    await db.SetAddAsync(cacheKey, redisKeys);
                    await db.KeyExpireAsync(cacheKey, TimeSpan.FromDays(30));
                    _memoryCache.Set(memKey, keysHash, TimeSpan.FromMinutes(1));
                }
            }
        }
    }
}
