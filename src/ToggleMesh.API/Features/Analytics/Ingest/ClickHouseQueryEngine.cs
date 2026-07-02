using System.Text.Json;
using ClickHouse.Client.ADO;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class ClickHouseQueryEngine : IAnalyticsQueryEngine
{
    private readonly string _connectionString;
    private readonly AppDbContext _db;
    private readonly ILogger<ClickHouseQueryEngine> _logger;

    public ClickHouseQueryEngine(IConfiguration configuration, AppDbContext db, ILogger<ClickHouseQueryEngine> logger)
    {
        _connectionString = configuration["Analytics:ClickHouse:ConnectionString"] 
            ?? throw new InvalidOperationException("ClickHouse ConnectionString not found");
        _db = db;
        _logger = logger;
    }

    public async Task AggregateMetricsAsync(CancellationToken ct = default)
    {
        await using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(ct);
        var activeRollouts = await _db.FlagEnvironmentStates
            .Include(f => f.FeatureFlag)
            .Select(f => new { f.EnvironmentId, f.FeatureFlag.Key })
            .ToListAsync(ct);

        if (activeRollouts.Count == 0) 
            return;

        var rolloutTuples = string.Join(",", activeRollouts.Select(r => $"('{r.EnvironmentId}', '{r.Key}')"));

        var query = $@"
            WITH exposed_users AS (
                SELECT 
                    EnvironmentId, 
                    FlagKey, 
                    Identity, 
                    Variant, 
                    MIN(Timestamp) as FirstExposureTimestamp
                FROM AnalyticsExposures
                WHERE (toString(EnvironmentId), FlagKey) IN ({rolloutTuples})
                GROUP BY EnvironmentId, FlagKey, Identity, Variant
            ),
        conversions AS (
            SELECT
                e.EnvironmentId,
                e.FlagKey,
                t.EventName,
                e.Variant,
                COUNT(DISTINCT e.Identity) as TotalConversions,
                SUM(t.Value) as TotalValue
            FROM exposed_users e
            JOIN AnalyticsTracks t 
              ON e.EnvironmentId = t.EnvironmentId 
             AND e.Identity = t.Identity
            WHERE t.Timestamp >= e.FirstExposureTimestamp
            GROUP BY e.EnvironmentId, e.FlagKey, t.EventName, e.Variant
        ),
        exposures_count AS (
            SELECT 
                EnvironmentId,
                FlagKey,
                Variant,
                COUNT(DISTINCT Identity) as TotalExposures
            FROM exposed_users
            GROUP BY EnvironmentId, FlagKey, Variant
        ),
        all_events AS (
            SELECT DISTINCT EnvironmentId, FlagKey, EventName 
            FROM conversions
        ),
        exposures_events AS (
            SELECT 
                e.EnvironmentId, 
                e.FlagKey, 
                e.Variant, 
                e.TotalExposures, 
                ae.EventName
            FROM exposures_count e
            JOIN all_events ae 
              ON e.EnvironmentId = ae.EnvironmentId 
             AND e.FlagKey = ae.FlagKey
        )
        SELECT 
            ee.EnvironmentId,
            ee.FlagKey,
            ee.EventName,
            ee.Variant,
            ee.TotalExposures,
            COALESCE(c.TotalConversions, 0) as TotalConversions,
            COALESCE(c.TotalValue, 0) as TotalValue
        FROM exposures_events ee
        LEFT JOIN conversions c 
          ON ee.EnvironmentId = c.EnvironmentId 
         AND ee.FlagKey = c.FlagKey 
         AND ee.Variant = c.Variant
         AND ee.EventName = c.EventName
        ";

        await using var command = connection.CreateCommand();
        command.CommandText = query;

        var metrics = new List<ExperimentMetric>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            metrics.Add(new ExperimentMetric
            {
                EnvironmentId = reader.GetGuid(0),
                FlagKey = reader.GetString(1),
                EventName = reader.GetString(2),
                Variant = reader.GetBoolean(3),
                TotalExposures = Convert.ToInt64(reader.GetValue(4)),
                TotalConversions = Convert.ToInt64(reader.GetValue(5)),
                LastCalculatedAt = DateTimeOffset.UtcNow
            });
        }

        if (metrics.Count > 0)
        {
            foreach (var m in metrics)
            {
                var existing = await _db.ExperimentMetrics.FirstOrDefaultAsync(x => 
                    x.EnvironmentId == m.EnvironmentId && 
                    x.FlagKey == m.FlagKey && 
                    x.EventName == m.EventName && 
                    x.Variant == m.Variant, ct);

                if (existing != null)
                {
                    existing.TotalExposures = m.TotalExposures;
                    existing.TotalConversions = m.TotalConversions;
                    existing.LastCalculatedAt = m.LastCalculatedAt;
                }
                else
                {
                    _db.ExperimentMetrics.Add(m);
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("[ClickHouseQueryEngine] Synced {Count} metric variants from ClickHouse to Postgres.", metrics.Count);
    }

    public async Task AggregateContextualMetricsAsync(CancellationToken ct = default)
    {
        var activeRollouts = await _db.FlagEnvironmentStates
            .Include(f => f.FeatureFlag)
            .Include(f => f.ContextualRollouts)
            .Where(f => f.ContextPartitionKeys != null && f.ContextPartitionKeys.Length > 0 && f.MabGoalEvent != null)
            .AsSplitQuery()
            .ToListAsync(ct);

        if (activeRollouts.Count == 0) return;

        await using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(ct);

        foreach (var state in activeRollouts)
        {
            var keys = state.ContextPartitionKeys;

            var exposures = new List<AnalyticsExposure>();
            var tracks = new List<AnalyticsTrack>();

            var exposureQuery = $"SELECT Id, EnvironmentId, FlagKey, Identity, Variant, Timestamp FROM AnalyticsExposures WHERE EnvironmentId = '{state.EnvironmentId}' AND FlagKey = '{state.FeatureFlag.Key}'";
            await using var expCommand = connection.CreateCommand();
            expCommand.CommandText = exposureQuery;
            await using var expReader = await expCommand.ExecuteReaderAsync(ct);
            while (await expReader.ReadAsync(ct))
            {
                exposures.Add(new AnalyticsExposure
                {
                    Id = expReader.GetGuid(0),
                    EnvironmentId = expReader.GetGuid(1),
                    FlagKey = expReader.GetString(2),
                    Identity = expReader.GetString(3),
                    Variant = expReader.GetBoolean(4),
                    Timestamp = expReader.GetDateTime(5),
                    Properties = null
                });
            }

            var tracksQuery = $@"
                SELECT t.Id, t.EnvironmentId, t.Identity, t.EventName, t.Value, t.Properties, t.Timestamp 
                FROM AnalyticsTracks t
                INNER JOIN (
                    SELECT DISTINCT Identity 
                    FROM AnalyticsExposures 
                    WHERE EnvironmentId = '{state.EnvironmentId}' AND FlagKey = '{state.FeatureFlag.Key}'
                ) e ON t.Identity = e.Identity
                WHERE t.EnvironmentId = '{state.EnvironmentId}' AND (t.EventName = '{state.MabGoalEvent}' OR t.Properties IS NOT NULL)";
            
            await using var trkCommand = connection.CreateCommand();
            trkCommand.CommandText = tracksQuery;
            await using var trkReader = await trkCommand.ExecuteReaderAsync(ct);
            while (await trkReader.ReadAsync(ct))
            {
                var valObj = trkReader.GetValue(4);
                float? val = valObj == DBNull.Value ? null : Convert.ToSingle(valObj);
                
                var propsStr = trkReader.GetString(5);
                JsonDocument? propsDoc = null;
                if (!string.IsNullOrEmpty(propsStr))
                {
                    try { propsDoc = JsonDocument.Parse(propsStr); }
                    catch
                    {
                        // ignored
                    }
                }

                tracks.Add(new AnalyticsTrack
                {
                    Id = trkReader.GetGuid(0),
                    EnvironmentId = trkReader.GetGuid(1),
                    Identity = trkReader.GetString(2),
                    EventName = trkReader.GetString(3),
                    Value = val,
                    Properties = propsDoc,
                    Timestamp = trkReader.GetDateTime(6)
                });
            }

            var tracksByIdentity = tracks
                .GroupBy(t => t.Identity)
                .ToDictionary(g => g.Key, g => g.ToList());

            var userExposures = exposures.GroupBy(e => new { e.Identity, e.Variant })
                .Select(g => {
                    var identity = g.Key.Identity;
                    var identityTracks = tracksByIdentity.TryGetValue(identity, out var list) ? list : new List<AnalyticsTrack>();
                    
                    var props = g.FirstOrDefault(e => e.Properties != null)?.Properties 
                        ?? identityTracks.Where(t => t.Properties != null).OrderBy(t => t.Timestamp).FirstOrDefault()?.Properties;

                    return new { 
                        g.Key.Identity, 
                        g.Key.Variant, 
                        FirstExposure = g.Min(e => e.Timestamp),
                        Properties = props
                    };
                }).ToList();

            var slices = userExposures.Select(e => GetContextSliceString(e.Properties, keys)).Distinct().ToList();

            foreach (var slice in slices)
            {
                var sliceUsers = userExposures.Where(e => GetContextSliceString(e.Properties, keys) == slice).ToList();
                var variants = new[] { false, true };

                foreach (var variant in variants)
                {
                    var variantUsers = sliceUsers.Where(u => u.Variant == variant).ToList();
                    if (variantUsers.Count == 0) continue;

                    var userIdentities = variantUsers.Select(u => u.Identity).ToHashSet();
                    var firstExposureLookup = variantUsers.ToDictionary(u => u.Identity, u => u.FirstExposure);
                    
                    var variantTracks = tracks
                        .Where(t => userIdentities.Contains(t.Identity) && t.EventName == state.MabGoalEvent)
                        .Where(t => t.Timestamp >= firstExposureLookup[t.Identity])
                        .ToList();

                    var totalExposures = variantUsers.Count;
                    var totalConversions = variantTracks.Select(t => t.Identity).Distinct().Count();
                    var totalValue = variantTracks.Sum(t => t.Value ?? 0);
                    var sumOfSquaredValues = variantTracks.Sum(t => (t.Value ?? 0) * (t.Value ?? 0));

                    var rolloutId = state.ContextualRollouts?.FirstOrDefault(r => r.ContextSlice == slice)?.Id;

                    var metric = await _db.ContextualExperimentMetrics.FirstOrDefaultAsync(m => 
                        m.EnvironmentId == state.EnvironmentId && 
                        m.FlagKey == state.FeatureFlag.Key && 
                        m.EventName == state.MabGoalEvent && 
                        m.Variant == variant && 
                        m.ContextSlice == slice &&
                        m.RolloutId == rolloutId, ct);

                    if (metric == null)
                    {
                        metric = new ContextualExperimentMetric
                        {
                            EnvironmentId = state.EnvironmentId,
                            FlagKey = state.FeatureFlag.Key,
                            EventName = state.MabGoalEvent ?? string.Empty,
                            Variant = variant,
                            RolloutId = rolloutId,
                            ContextSlice = slice,
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.ContextualExperimentMetrics.Add(metric);
                    }

                    metric.TotalExposures = totalExposures;
                    metric.TotalConversions = totalConversions;
                    metric.TotalValue = totalValue;
                    metric.SumOfSquaredValues = sumOfSquaredValues;
                    metric.LastCalculatedAt = DateTimeOffset.UtcNow;
                }
            }
        }
        
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("[ClickHouseQueryEngine] Synced Contextual Experiment Metrics from ClickHouse to Postgres.");
    }

    private string GetContextSliceString(JsonDocument? properties, string[] keys)
    {
        if (properties == null) return "{}";
        var dict = new Dictionary<string, string>();
        foreach (var key in keys)
        {
            if (properties.RootElement.TryGetProperty(key, out var el))
            {
                dict[key] = el.ToString();
            }
            else
            {
                dict[key] = "null";
            }
        }
        return JsonSerializer.Serialize(dict);
    }

    public async Task<IEnumerable<(DateTime Time, long Count)>> GetProjectHourlyEvaluationsAsync(Guid projectId, IEnumerable<Guid> environmentIds, TimeSpan duration, CancellationToken ct = default)
    {
        var envIdsStr = string.Join(",", environmentIds.Select(id => $"'{id}'"));
        if (string.IsNullOrEmpty(envIdsStr)) 
            return [];

        var query = $@"
            SELECT 
                toStartOfHour(Timestamp) as HourTime,
                COUNT() as EvalCount
            FROM AnalyticsExposures
            WHERE EnvironmentId IN ({envIdsStr})
              AND Timestamp >= now() - INTERVAL {(int)duration.TotalHours} HOUR
            GROUP BY HourTime
            ORDER BY HourTime ASC
        ";

        await using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = query;

        var result = new List<(DateTime, long)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetDateTime(0), Convert.ToInt64(reader.GetValue(1))));

        return result;
    }

    public async Task<IEnumerable<ExperimentTimeSeriesPoint>> GetExperimentTimeSeriesAsync(Guid environmentId, string flagKey, string eventName, TimeSpan duration, CancellationToken ct = default)
    {
        var query = $@"
            WITH hourly_exposures AS (
                SELECT 
                    toStartOfMinute(Timestamp) as TimeBucket,
                    Variant,
                    uniqExact(Identity) as Exposures
                FROM AnalyticsExposures
                WHERE EnvironmentId = '{environmentId}' 
                  AND FlagKey = '{flagKey}' 
                  AND Timestamp >= now() - INTERVAL {(int)duration.TotalSeconds} SECOND
                GROUP BY TimeBucket, Variant
            ),
            exposed_users AS (
                SELECT Identity, Variant, MIN(Timestamp) as FirstExposure
                FROM AnalyticsExposures
                WHERE EnvironmentId = '{environmentId}' 
                  AND FlagKey = '{flagKey}' 
                  AND Timestamp >= now() - INTERVAL {(int)duration.TotalSeconds} SECOND
                GROUP BY Identity, Variant
            ),
            hourly_conversions AS (
                SELECT 
                    toStartOfMinute(t.Timestamp) as TimeBucket,
                    e.Variant as Variant,
                    uniqExact(t.Identity) as Conversions
                FROM AnalyticsTracks t
                INNER JOIN exposed_users e ON t.Identity = e.Identity
                WHERE t.EnvironmentId = '{environmentId}' 
                  AND t.EventName = '{eventName}' 
                  AND t.Timestamp >= e.FirstExposure
                GROUP BY TimeBucket, Variant
            )
            SELECT 
                if(isNull(e.TimeBucket), c.TimeBucket, e.TimeBucket) as TimeBucket,
                if(isNull(e.Variant), c.Variant, e.Variant) as Variant,
                if(isNull(e.Exposures), 0, e.Exposures) as Exposures,
                if(isNull(c.Conversions), 0, c.Conversions) as Conversions
            FROM hourly_exposures e
            FULL OUTER JOIN hourly_conversions c 
              ON e.TimeBucket = c.TimeBucket AND e.Variant = c.Variant
            ORDER BY TimeBucket ASC, Variant ASC
        ";

        await using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = query;

        var result = new List<ExperimentTimeSeriesPoint>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var timeBucket = reader.GetDateTime(0);
            var variant = reader.GetBoolean(1);
            var exposures = Convert.ToInt64(reader.GetValue(2));
            var conversions = Convert.ToInt64(reader.GetValue(3));
            result.Add(new ExperimentTimeSeriesPoint(timeBucket, variant, exposures, conversions));
        }

        return result;
    }
}
