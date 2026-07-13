using System.Diagnostics;
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
            .Where(f => f.IsExperimentActive)
            .Select(f => new { f.EnvironmentId, f.FeatureFlag.Key, f.ExperimentStartedAt })
            .ToListAsync(ct);

        if (activeRollouts.Count == 0) 
            return;

        var conditions = activeRollouts.Select(r => 
            $"(toString(EnvironmentId) = '{r.EnvironmentId}' AND FlagKey = '{r.Key.Replace("'", "''")}' AND Timestamp >= '{r.ExperimentStartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "1970-01-01 00:00:00"}')"
        );
        var whereClause = string.Join(" OR ", conditions);

        var query = $@"
            WITH exposed_users AS (
                SELECT 
                    EnvironmentId, 
                    FlagKey, 
                    Identity, 
                    VariationId, 
                    MIN(Timestamp) as FirstExposureTimestamp
                FROM AnalyticsExposures
                WHERE {whereClause}
                GROUP BY EnvironmentId, FlagKey, Identity, VariationId
            ),
        conversions AS (
            SELECT
                e.EnvironmentId,
                e.FlagKey,
                t.EventName,
                e.VariationId,
                uniqExact(t.Identity) as TotalConversions,
                SUM(t.Value) as TotalValue,
                SUM(t.Value * t.Value) as SumOfSquaredValues
            FROM exposed_users e
            JOIN AnalyticsTracks t 
              ON e.EnvironmentId = t.EnvironmentId 
             AND e.Identity = t.Identity
            WHERE t.Timestamp >= e.FirstExposureTimestamp
            GROUP BY e.EnvironmentId, e.FlagKey, t.EventName, e.VariationId
        ),
        exposures_count AS (
            SELECT 
                EnvironmentId,
                FlagKey,
                VariationId,
                uniqExact(Identity) as TotalExposures
            FROM exposed_users
            GROUP BY EnvironmentId, FlagKey, VariationId
        ),
        all_events AS (
            SELECT DISTINCT EnvironmentId, FlagKey, EventName 
            FROM conversions
        ),
        exposures_events AS (
            SELECT 
                e.EnvironmentId, 
                e.FlagKey, 
                e.VariationId, 
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
            ee.VariationId,
            ee.TotalExposures,
            COALESCE(c.TotalConversions, toUInt64(0)) as TotalConversions,
            COALESCE(c.TotalValue, toFloat64(0)) as TotalValue,
            COALESCE(c.SumOfSquaredValues, toFloat64(0)) as SumOfSquaredValues
        FROM exposures_events ee
        LEFT JOIN conversions c 
          ON ee.EnvironmentId = c.EnvironmentId 
         AND ee.FlagKey = c.FlagKey 
         AND ee.VariationId = c.VariationId
         AND ee.EventName = c.EventName
        ";

        await using var command = connection.CreateCommand();
        command.CommandText = query;

        var sw = Stopwatch.StartNew();
        var metrics = new List<ExperimentMetric>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            metrics.Add(new ExperimentMetric
            {
                EnvironmentId = reader.GetGuid(0),
                FlagKey = reader.GetString(1),
                EventName = reader.GetString(2),
                VariationId = reader.GetGuid(3),
                TotalExposures = Convert.ToInt64(reader.GetValue(4)),
                TotalConversions = Convert.ToInt64(reader.GetValue(5)),
                LastCalculatedAt = DateTimeOffset.UtcNow
            });
        }
        sw.Stop();
        _logger.LogDebug("[ClickHouseQueryEngine.AggregateMetricsAsync] ClickHouse query and parse took {Ms}ms", sw.ElapsedMilliseconds);

        if (metrics.Count > 0)
        {
            var envIds = metrics.Select(m => m.EnvironmentId).Distinct().ToList();
            var flagKeys = metrics.Select(m => m.FlagKey).Distinct().ToList();

            var existingMetrics = await _db.ExperimentMetrics
                .Where(x => envIds.Contains(x.EnvironmentId) && flagKeys.Contains(x.FlagKey))
                .ToDictionaryAsync(x => $"{x.EnvironmentId}_{x.FlagKey}_{x.EventName}_{x.VariationId}", ct);

            foreach (var m in metrics)
            {
                var key = $"{m.EnvironmentId}_{m.FlagKey}_{m.EventName}_{m.VariationId}";
                
                if (existingMetrics.TryGetValue(key, out var existing))
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

            var pgSw = Stopwatch.StartNew();
            await _db.SaveChangesAsync(ct);
            pgSw.Stop();
            _logger.LogDebug("[ClickHouseQueryEngine.AggregateMetricsAsync] Postgres UPSERT took {Ms}ms", pgSw.ElapsedMilliseconds);
        }

        _logger.LogInformation("[ClickHouseQueryEngine] Synced {Count} metric variants from ClickHouse to Postgres.", metrics.Count);
    }

    public async Task AggregateContextualMetricsAsync(CancellationToken ct = default)
    {
        var activeRollouts = await _db.FlagEnvironmentStates
            .Include(f => f.FeatureFlag)
            .Include(f => f.ContextualRollouts)
            .Where(f => f.ContextPartitionKeys.Length > 0 && f.MabGoalEvent != null)
            .AsSplitQuery()
            .ToListAsync(ct);

        if (activeRollouts.Count == 0) return;

        var allMetricsData = new List<(ToggleMesh.API.Features.Flags.Domain.FlagEnvironmentState State, List<(Guid VariationId, long Exposures, long Conversions, double Value, double SumSquared, string Slice)> Results)>();
        await using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(ct);

        foreach (var state in activeRollouts)
        {
            var keys = state.ContextPartitionKeys;
            var startedAtStr = state.ExperimentStartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "1970-01-01 00:00:00";

            var selectExtracts = string.Join(", ", keys.Select((k, i) => $"if(empty(Properties), 'null', if(JSONHas(Properties, '{k.Replace("'", "''")}'), JSONExtractString(Properties, '{k.Replace("'", "''")}'), 'null')) as Key{i}"));
            var groupByKeys = string.Join(", ", keys.Select((_, i) => $"Key{i}"));

            var query = $@"
                WITH user_exposures AS (
                    SELECT 
                        Identity, 
                        VariationId, 
                        MIN(Timestamp) as FirstExposure,
                        anyIf(Properties, Properties != '' AND Properties != 'null') as ExpProps
                    FROM AnalyticsExposures
                    WHERE EnvironmentId = {{envId:String}} AND FlagKey = {{flagKey:String}} AND Timestamp >= {{startedAt:String}}
                    GROUP BY Identity, VariationId
                ),
                user_tracks AS (
                    SELECT 
                        Identity,
                        anyIf(Properties, Properties != '' AND Properties != 'null') as TrackProps
                    FROM AnalyticsTracks
                    WHERE EnvironmentId = {{envId:String}} AND Properties != '' AND Properties != 'null'
                    GROUP BY Identity
                ),
                user_slices AS (
                    SELECT 
                        e.Identity,
                        e.VariationId,
                        e.FirstExposure,
                        if(empty(e.ExpProps), t.TrackProps, e.ExpProps) as Properties
                    FROM user_exposures e
                    LEFT JOIN user_tracks t ON e.Identity = t.Identity
                ),
                user_slices_extracted AS (
                    SELECT 
                        Identity,
                        VariationId,
                        FirstExposure,
                        {selectExtracts}
                    FROM user_slices
                ),
                conversions AS (
                    SELECT
                        s.VariationId,
                        uniqExact(t.Identity) as TotalConversions,
                        SUM(t.Value) as TotalValue,
                        SUM(t.Value * t.Value) as SumOfSquaredValues,
                        {groupByKeys}
                    FROM user_slices_extracted s
                    JOIN AnalyticsTracks t 
                      ON t.Identity = s.Identity 
                    WHERE t.EnvironmentId = {{envId:String}} 
                      AND t.EventName = {{goalEvent:String}} 
                      AND t.Timestamp >= s.FirstExposure
                    GROUP BY s.VariationId, {groupByKeys}
                ),
                exposures_count AS (
                    SELECT
                        VariationId,
                        uniqExact(Identity) as TotalExposures,
                        {groupByKeys}
                    FROM user_slices_extracted
                    GROUP BY VariationId, {groupByKeys}
                )
                SELECT 
                    e.VariationId, 
                    e.TotalExposures, 
                    COALESCE(c.TotalConversions, toUInt64(0)) as TotalConversions, 
                    COALESCE(c.TotalValue, toFloat64(0)) as TotalValue, 
                    COALESCE(c.SumOfSquaredValues, toFloat64(0)) as SumOfSquaredValues, 
                    {string.Join(", ", keys.Select((_, i) => $"e.Key{i}"))}
                FROM exposures_count e
                LEFT JOIN conversions c ON e.VariationId = c.VariationId AND {string.Join(" AND ", keys.Select((_, i) => $"e.Key{i} = c.Key{i}"))}
            ";

            var results = new List<(Guid VariationId, long Exposures, long Conversions, double Value, double SumSquared, string Slice)>();

            var sw = Stopwatch.StartNew();
            await using var command = connection.CreateCommand();
            command.CommandText = query;

            var pEnv = command.CreateParameter(); pEnv.ParameterName = "envId"; pEnv.Value = state.EnvironmentId.ToString(); command.Parameters.Add(pEnv);
            var pFlag = command.CreateParameter(); pFlag.ParameterName = "flagKey"; pFlag.Value = state.FeatureFlag.Key; command.Parameters.Add(pFlag);
            var pEvent = command.CreateParameter(); pEvent.ParameterName = "goalEvent"; pEvent.Value = state.MabGoalEvent ?? ""; command.Parameters.Add(pEvent);
            var pStartedAt = command.CreateParameter(); pStartedAt.ParameterName = "startedAt"; pStartedAt.Value = startedAtStr; command.Parameters.Add(pStartedAt);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var variationId = reader.GetGuid(0);
                var totalExposures = Convert.ToInt64(reader.GetValue(1));
                var totalConversions = Convert.ToInt64(reader.GetValue(2));
                var totalValue = reader.IsDBNull(3) ? 0.0 : Convert.ToDouble(reader.GetValue(3));
                var sumOfSquaredValues = reader.IsDBNull(4) ? 0.0 : Convert.ToDouble(reader.GetValue(4));

                var dict = new Dictionary<string, string>();
                for (int i = 0; i < keys.Length; i++)
                {
                    var val = reader.IsDBNull(5 + i) ? "null" : reader.GetString(5 + i);
                    dict[keys[i]] = val;
                }

                results.Add((variationId, totalExposures, totalConversions, totalValue, sumOfSquaredValues, JsonSerializer.Serialize(dict)));
            }
            sw.Stop();
            _logger.LogDebug("[ClickHouseQueryEngine.AggregateContextualMetricsAsync] ClickHouse query and parse for flag {Flag} took {Ms}ms", state.FeatureFlag.Key, sw.ElapsedMilliseconds);

            allMetricsData.Add((state, results));
        }

        var pgSw = Stopwatch.StartNew();
        
        foreach (var data in allMetricsData)
        {
            if (data.Results.Count == 0) continue;

            var state = data.State;
            var existingMetrics = await _db.ContextualExperimentMetrics
                .Where(x => x.EnvironmentId == state.EnvironmentId && x.FlagKey == state.FeatureFlag.Key && x.EventName == state.MabGoalEvent)
                .ToDictionaryAsync(x => $"{x.VariationId}_{x.ContextSlice}_{x.RolloutId}", ct);

            foreach (var r in data.Results)
            {
                var rolloutId = state.ContextualRollouts?.FirstOrDefault(x => x.ContextSlice == r.Slice)?.Id;
                var key = $"{r.VariationId}_{r.Slice}_{rolloutId}";

                if (existingMetrics.TryGetValue(key, out var metric))
                {
                    metric.TotalExposures = r.Exposures;
                    metric.TotalConversions = r.Conversions;
                    metric.TotalValue = r.Value;
                    metric.SumOfSquaredValues = r.SumSquared;
                    metric.LastCalculatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    metric = new ContextualExperimentMetric
                    {
                        EnvironmentId = state.EnvironmentId,
                        FlagKey = state.FeatureFlag.Key,
                        EventName = state.MabGoalEvent ?? string.Empty,
                        VariationId = r.VariationId,
                        RolloutId = rolloutId,
                        ContextSlice = r.Slice,
                        CreatedAt = DateTime.UtcNow,
                        TotalExposures = r.Exposures,
                        TotalConversions = r.Conversions,
                        TotalValue = r.Value,
                        SumOfSquaredValues = r.SumSquared,
                        LastCalculatedAt = DateTimeOffset.UtcNow
                    };
                    _db.ContextualExperimentMetrics.Add(metric);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        pgSw.Stop();
        _logger.LogDebug("[ClickHouseQueryEngine.AggregateContextualMetricsAsync] Postgres UPSERT for all metrics took {Ms}ms", pgSw.ElapsedMilliseconds);

        _logger.LogInformation("[ClickHouseQueryEngine] Synced Contextual Experiment Metrics from ClickHouse to Postgres.");
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
                    VariationId,
                    uniqExact(Identity) as Exposures
                FROM AnalyticsExposures
                WHERE EnvironmentId = {{envId:String}} 
                  AND FlagKey = {{flagKey:String}} 
                  AND Timestamp >= now() - INTERVAL {(int)duration.TotalSeconds} SECOND
                GROUP BY TimeBucket, VariationId
            ),
            exposed_users AS (
                SELECT Identity, VariationId, MIN(Timestamp) as FirstExposure
                FROM AnalyticsExposures
                WHERE EnvironmentId = {{envId:String}} 
                  AND FlagKey = {{flagKey:String}} 
                  AND Timestamp >= now() - INTERVAL {(int)duration.TotalSeconds} SECOND
                GROUP BY Identity, VariationId
            ),
            hourly_conversions AS (
                SELECT 
                    toStartOfMinute(t.Timestamp) as TimeBucket,
                    e.VariationId as VariationId,
                    uniqExact(t.Identity) as Conversions
                FROM AnalyticsTracks t
                INNER JOIN exposed_users e ON t.Identity = e.Identity
                WHERE t.EnvironmentId = {{envId:String}} 
                  AND t.EventName = {{eventName:String}} 
                  AND t.Timestamp >= e.FirstExposure
                GROUP BY TimeBucket, VariationId
            )
            SELECT 
                if(isNull(e.TimeBucket), c.TimeBucket, e.TimeBucket) as TimeBucket,
                if(isNull(e.VariationId), c.VariationId, e.VariationId) as VariationId,
                if(isNull(e.Exposures), 0, e.Exposures) as Exposures,
                if(isNull(c.Conversions), 0, c.Conversions) as Conversions
            FROM hourly_exposures e
            FULL OUTER JOIN hourly_conversions c 
              ON e.TimeBucket = c.TimeBucket AND e.VariationId = c.VariationId
            ORDER BY TimeBucket ASC, VariationId ASC
        ";

        await using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = query;

        var pEnv = command.CreateParameter(); 
        pEnv.ParameterName = "envId"; 
        pEnv.Value = environmentId.ToString(); 
        command.Parameters.Add(pEnv);
        var pFlag = command.CreateParameter(); 
        pFlag.ParameterName = "flagKey"; 
        pFlag.Value = flagKey; 
        command.Parameters.Add(pFlag);
        var pEvent = command.CreateParameter(); 
        pEvent.ParameterName = "eventName"; 
        pEvent.Value = eventName; 
        command.Parameters.Add(pEvent);

        var result = new List<ExperimentTimeSeriesPoint>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var timeBucket = reader.GetDateTime(0);
            var variationId = reader.GetGuid(1);
            var exposures = Convert.ToInt64(reader.GetValue(2));
            var conversions = Convert.ToInt64(reader.GetValue(3));
            result.Add(new ExperimentTimeSeriesPoint(timeBucket, variationId, exposures, conversions));
        }

        return result;
    }
}
