using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Infrastructure.Caching;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class KafkaAnalyticsConsumerWorker : BackgroundService
{
    private readonly ILogger<KafkaAnalyticsConsumerWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly string _topic;
    private readonly string _bootstrapServers;
    private readonly string _groupId;

    public KafkaAnalyticsConsumerWorker(IConfiguration configuration, ILogger<KafkaAnalyticsConsumerWorker> logger, IServiceScopeFactory scopeFactory, IMemoryCache memoryCache)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        
        _topic = configuration["Analytics:Kafka:Topic"] ?? "togglemesh-events";
        _bootstrapServers = configuration["Analytics:Kafka:BootstrapServers"] ?? "localhost:9092";
        _groupId = configuration["Analytics:Kafka:GroupId"] ?? "togglemesh-analytics-consumer-group";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Kafka consumer encountered an error: {Message}. Restarting in 5 seconds...", ex.Message);
                try { await Task.Delay(5000, stoppingToken); } catch { /* ignore */ }
            }
        }
    }

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = _groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_topic);

        _logger.LogInformation("Kafka Consumer started for topic {Topic} at {Servers}", _topic, _bootstrapServers);

        try
        {
            var batchExposures = new List<AnalyticsExposure>();
            var batchTracks = new List<AnalyticsTrack>();
            var lastCommitTime = DateTime.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? consumeResult = null;
                try
                {
                    consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(100));
                }
                catch (ConsumeException ex) when (ex.Error.Reason.Contains("Unknown topic"))
                {
                    _logger.LogWarning("Kafka topic {Topic} not available yet. Waiting for it to be created...", _topic);
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming from Kafka");
                    continue;
                }

                if (consumeResult?.Message != null)
                {
                    var payload = JsonSerializer.Deserialize<KafkaMessagePayload>(consumeResult.Message.Value);
                    if (payload is { Events.Count: > 0 })
                    {
                        foreach (var e in payload.Events)
                        {
                            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp);
                            if (e.Type == AnalyticsEventType.Exposure)
                            {
                                batchExposures.Add(new AnalyticsExposure
                                {
                                    Id = Guid.CreateVersion7(),
                                    EnvironmentId = payload.EnvironmentId,
                                    FlagKey = e.FlagKey!,
                                    Identity = e.Identity,
                                    Variant = e.Result,
                                    Properties = e.Properties != null ? JsonDocument.Parse(JsonSerializer.Serialize(e.Properties)) : null,
                                    Timestamp = timestamp
                                });
                            }
                            else
                            {
                                batchTracks.Add(new AnalyticsTrack
                                {
                                    Id = Guid.CreateVersion7(),
                                    EnvironmentId = payload.EnvironmentId,
                                    Identity = e.Identity,
                                    EventName = e.EventName!,
                                    Value = e.Value,
                                    Properties = e.Properties != null ? JsonDocument.Parse(JsonSerializer.Serialize(e.Properties)) : null,
                                    Timestamp = timestamp
                                });
                            }
                        }
                    }
                }

                if (batchExposures.Count + batchTracks.Count > 10000 || (DateTime.UtcNow - lastCommitTime).TotalSeconds > 5)
                {
                    if (batchExposures.Count > 0 || batchTracks.Count > 0)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var sink = scope.ServiceProvider.GetRequiredService<IAnalyticsStorageSink>();

                        await sink.WriteBatchAsync(batchExposures, batchTracks, stoppingToken);

                        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
                        var db = redis.GetDatabase();

                        var allEvents = batchTracks
                            .Select(t => new { t.EnvironmentId, t.EventName })
                            .Distinct()
                            .ToList();

                        foreach (var ev in allEvents)
                        {
                            await db.SetAddAsync(CacheKeys.UniqueEvents(ev.EnvironmentId), ev.EventName);
                        }

                        var eventsWithValue = batchTracks
                            .Where(t => t.Value.HasValue)
                            .Select(t => new { t.EnvironmentId, t.EventName })
                            .Distinct()
                            .ToList();

                        foreach (var ev in eventsWithValue)
                        {
                            var cacheKey = CacheKeys.EventSchemaHasValue(ev.EnvironmentId, ev.EventName);
                            await db.StringSetAsync(cacheKey, "true", TimeSpan.FromDays(30));
                        }

                        var flagsWithContext = batchExposures
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

                                if (_memoryCache.TryGetValue(memKey, out string? lastHash) && lastHash == keysHash)
                                    continue;

                                var redisKeys = keys.Select(k => (RedisValue)k).ToArray();
                                await db.SetAddAsync(cacheKey, redisKeys);
                                await db.KeyExpireAsync(cacheKey, TimeSpan.FromDays(30));
                                _memoryCache.Set(memKey, keysHash, TimeSpan.FromMinutes(1));
                            }
                        }

                        consumer.Commit();
                    }
                    
                    batchExposures.Clear();
                    batchTracks.Clear();
                    lastCommitTime = DateTime.UtcNow;
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
