using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class PostgresQueryEngine : IAnalyticsQueryEngine
{
    private readonly AppDbContext _db;

    public PostgresQueryEngine(AppDbContext db)
    {
        _db = db;
        _db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
    }

    public async Task AggregateMetricsAsync(CancellationToken ct = default)
    {
        var sql = @"
            WITH active_rollouts AS (
                SELECT fes.""EnvironmentId"", ff.""Key"" as ""FlagKey"", fes.""ExperimentStartedAt""
                FROM ""FlagEnvironmentStates"" fes
                JOIN ""ProjectFeatureFlags"" ff ON ff.""Id"" = fes.""FeatureFlagId""
                WHERE fes.""IsExperimentActive"" = true
            ),
            exposed_users AS (
                SELECT 
                    e.""EnvironmentId"", 
                    e.""FlagKey"", 
                    e.""Identity"", 
                    e.""VariationId"", 
                    MIN(e.""Timestamp"") as ""FirstExposureTimestamp""
                FROM ""AnalyticsExposures"" e
                JOIN active_rollouts ar 
                  ON e.""EnvironmentId"" = ar.""EnvironmentId"" 
                 AND e.""FlagKey"" = ar.""FlagKey""
                WHERE e.""Timestamp"" >= COALESCE(ar.""ExperimentStartedAt"", '1970-01-01'::timestamp)
                GROUP BY e.""EnvironmentId"", e.""FlagKey"", e.""Identity"", e.""VariationId""
            ),
            conversions AS (
                SELECT
                    e.""EnvironmentId"",
                    e.""FlagKey"",
                    t.""EventName"",
                    e.""VariationId"",
                    COUNT(DISTINCT e.""Identity"") as ""TotalConversions"",
                    SUM(COALESCE(t.""Value"", 0)) as ""TotalValue"",
                    SUM(COALESCE(t.""Value"", 0) * COALESCE(t.""Value"", 0)) as ""SumOfSquaredValues""
                FROM exposed_users e
                JOIN ""AnalyticsTracks"" t 
                  ON e.""EnvironmentId"" = t.""EnvironmentId"" 
                 AND e.""Identity"" = t.""Identity""
                 AND t.""Timestamp"" >= e.""FirstExposureTimestamp""
                JOIN active_rollouts ar
                  ON e.""EnvironmentId"" = ar.""EnvironmentId""
                 AND e.""FlagKey"" = ar.""FlagKey""
                WHERE t.""Timestamp"" >= COALESCE(ar.""ExperimentStartedAt"", '1970-01-01'::timestamp)
                GROUP BY e.""EnvironmentId"", e.""FlagKey"", t.""EventName"", e.""VariationId""
            ),
            exposures_count AS (
                SELECT 
                    ""EnvironmentId"",
                    ""FlagKey"",
                    ""VariationId"",
                    COUNT(DISTINCT ""Identity"") as ""TotalExposures""
                FROM exposed_users
                GROUP BY ""EnvironmentId"", ""FlagKey"", ""VariationId""
            )
            INSERT INTO ""ExperimentMetrics"" (""Id"", ""EnvironmentId"", ""FlagKey"", ""EventName"", ""VariationId"", ""TotalExposures"", ""TotalConversions"", ""TotalValue"", ""SumOfSquaredValues"", ""LastCalculatedAt"", ""CreatedAt"")
            SELECT 
                gen_random_uuid(),
                e.""EnvironmentId"",
                e.""FlagKey"",
                c.""EventName"",
                e.""VariationId"",
                e.""TotalExposures"",
                COALESCE(c.""TotalConversions"", 0),
                COALESCE(c.""TotalValue"", 0),
                COALESCE(c.""SumOfSquaredValues"", 0),
                now(),
                now()
            FROM exposures_count e
            JOIN conversions c 
              ON e.""EnvironmentId"" = c.""EnvironmentId"" 
             AND e.""FlagKey"" = c.""FlagKey"" 
             AND e.""VariationId"" = c.""VariationId""
            UNION ALL
            SELECT 
                gen_random_uuid(),
                ""EnvironmentId"",
                ""FlagKey"",
                '$exposure',
                ""VariationId"",
                ""TotalExposures"",
                0,
                0,
                0,
                now(),
                now()
            FROM exposures_count
            ON CONFLICT (""EnvironmentId"", ""FlagKey"", ""EventName"", ""VariationId"") 
            DO UPDATE SET 
                ""TotalExposures"" = EXCLUDED.""TotalExposures"",
                ""TotalConversions"" = EXCLUDED.""TotalConversions"",
                ""TotalValue"" = EXCLUDED.""TotalValue"",
                ""SumOfSquaredValues"" = EXCLUDED.""SumOfSquaredValues"",
                ""LastCalculatedAt"" = EXCLUDED.""LastCalculatedAt"",
                ""UpdatedAt"" = now();
        ";

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    public async Task AggregateContextualMetricsAsync(CancellationToken ct = default)
    {
        var activeRollouts = await _db.FlagEnvironmentStates
            .AsNoTracking()
            .Include(f => f.FeatureFlag)
            .Include(f => f.ContextualRollouts)
            .Where(f => f.ContextPartitionKeys.Length > 0 && f.MabGoalEvent != null)
            .AsSplitQuery()
            .ToListAsync(ct);

        if (activeRollouts.Count == 0) 
            return;

        var connection = _db.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed) await connection.OpenAsync(ct);

        try
        {
            foreach (var state in activeRollouts)
            {
                var keys = state.ContextPartitionKeys;
                var selectExtracts = string.Join(", ", keys.Select((k, i) => $"COALESCE(\"Properties\"->>'{k.Replace("'", "''")}', 'null') as \"Key{i}\""));
                var groupByKeys = string.Join(", ", keys.Select((_, i) => $"\"Key{i}\""));

                var sql = $"""
                    WITH user_props AS (
                        SELECT 
                            e."Identity", 
                            e."VariationId", 
                            MIN(e."Timestamp") as "FirstExposure",
                            COALESCE(
                                (SELECT "Properties" FROM "AnalyticsExposures" e2 WHERE e2."EnvironmentId" = @envId AND e2."FlagKey" = @flagKey AND e2."Identity" = e."Identity" AND e2."Properties" IS NOT NULL LIMIT 1),
                                (SELECT "Properties" FROM "AnalyticsTracks" t2 WHERE t2."EnvironmentId" = @envId AND t2."Identity" = e."Identity" AND t2."Properties" IS NOT NULL ORDER BY t2."Timestamp" LIMIT 1)
                            ) as "Properties"
                        FROM "AnalyticsExposures" e
                        WHERE e."EnvironmentId" = @envId AND e."FlagKey" = @flagKey AND e."Timestamp" >= @startedAt
                        GROUP BY e."Identity", e."VariationId"
                    ),
                    user_slices AS (
                        SELECT 
                            "Identity",
                            "VariationId",
                            "FirstExposure",
                            {selectExtracts}
                        FROM user_props
                    ),
                    conversions AS (
                        SELECT
                            s."VariationId",
                            COUNT(DISTINCT s."Identity") as "TotalExposures",
                            COUNT(DISTINCT t."Identity") as "TotalConversions",
                            SUM(COALESCE(t."Value", 0)) as "TotalValue",
                            SUM(COALESCE(t."Value", 0) * COALESCE(t."Value", 0)) as "SumOfSquaredValues",
                            {groupByKeys}
                        FROM user_slices s
                        LEFT JOIN "AnalyticsTracks" t 
                          ON t."EnvironmentId" = @envId 
                            AND t."Identity" = s."Identity" 
                            AND t."EventName" = @goalEvent 
                            AND t."Timestamp" >= s."FirstExposure"
                        WHERE t."Timestamp" IS NULL OR t."Timestamp" >= @startedAt
                        GROUP BY s."VariationId", {groupByKeys}
                    )
                    SELECT "VariationId", "TotalExposures", "TotalConversions", "TotalValue", "SumOfSquaredValues", {groupByKeys} FROM conversions;             
                    """;

                var results = new List<(Guid VariationId, long Exposures, long Conversions, double Value, double SumSquared, string Slice)>();

                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    
                    var pEnv = command.CreateParameter(); pEnv.ParameterName = "@envId"; pEnv.Value = state.EnvironmentId; command.Parameters.Add(pEnv);
                    var pFlag = command.CreateParameter(); pFlag.ParameterName = "@flagKey"; pFlag.Value = state.FeatureFlag.Key; command.Parameters.Add(pFlag);
                    var pEvent = command.CreateParameter(); pEvent.ParameterName = "@goalEvent"; pEvent.Value = state.MabGoalEvent ?? ""; command.Parameters.Add(pEvent);
                    var pStartedAt = command.CreateParameter(); pStartedAt.ParameterName = "@startedAt"; pStartedAt.Value = state.ExperimentStartedAt ?? DateTimeOffset.FromUnixTimeSeconds(0); command.Parameters.Add(pStartedAt);

                    using var reader = await command.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        var variationId = reader.GetGuid(0);
                        var totalExposures = reader.GetInt64(1);
                        var totalConversions = reader.GetInt64(2);
                        var totalValue = reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3);
                        var sumOfSquaredValues = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4);

                        var dict = new Dictionary<string, string>();
                        for (int i = 0; i < keys.Length; i++)
                        {
                            var val = reader.IsDBNull(5 + i) ? "null" : reader.GetString(5 + i);
                            dict[keys[i]] = val;
                        }

                        results.Add((variationId, totalExposures, totalConversions, totalValue, sumOfSquaredValues, JsonSerializer.Serialize(dict)));
                    }
                }

                if (results.Count > 0)
                {
                    var existingMetrics = await _db.ContextualExperimentMetrics
                        .Where(x => x.EnvironmentId == state.EnvironmentId && x.FlagKey == state.FeatureFlag.Key && x.EventName == state.MabGoalEvent)
                        .ToDictionaryAsync(x => $"{x.VariationId}_{x.ContextSlice}_{x.RolloutId}", ct);

                    foreach (var r in results)
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
            }

            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }

    public async Task<IEnumerable<(DateTime Time, long Count)>> GetProjectHourlyEvaluationsAsync(Guid projectId, IEnumerable<Guid> environmentIds, TimeSpan duration, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.Subtract(duration);
        
        var query = await _db.FlagMetricBuckets
            .Where(b => environmentIds.Contains(b.EnvironmentId) && b.TimestampBucket >= cutoff)
            .GroupBy(b => b.TimestampBucket)
            .Select(g => new
            {
                Time = g.Key,
                Count = g.Sum(x => x.Count)
            })
            .OrderBy(x => x.Time)
            .ToListAsync(ct);

        return query.Select(x => (x.Time.UtcDateTime, x.Count));
    }

    public async Task<IEnumerable<ExperimentTimeSeriesPoint>> GetExperimentTimeSeriesAsync(Guid environmentId, string flagKey, string eventName, TimeSpan duration, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(duration);
        var results = new List<ExperimentTimeSeriesPoint>();

        var connection = _db.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                WITH hourly_exposures AS (
                    SELECT 
                        DATE_TRUNC('minute', ""Timestamp"") as ""TimeBucket"",
                        ""VariationId"",
                        COUNT(DISTINCT ""Identity"") as ""Exposures""
                    FROM ""AnalyticsExposures""
                    WHERE ""EnvironmentId"" = @envId AND ""FlagKey"" = @flagKey AND ""Timestamp"" >= @cutoff
                    GROUP BY 1, 2
                ),
                exposed_users AS (
                    SELECT ""Identity"", ""VariationId"", MIN(""Timestamp"") as ""FirstExposure""
                    FROM ""AnalyticsExposures""
                    WHERE ""EnvironmentId"" = @envId AND ""FlagKey"" = @flagKey AND ""Timestamp"" >= @cutoff
                    GROUP BY 1, 2
                ),
                hourly_conversions AS (
                    SELECT 
                        DATE_TRUNC('minute', t.""Timestamp"") as ""TimeBucket"",
                        e.""VariationId"",
                        COUNT(DISTINCT t.""Identity"") as ""Conversions""
                    FROM ""AnalyticsTracks"" t
                    JOIN exposed_users e ON t.""Identity"" = e.""Identity""
                    WHERE t.""EnvironmentId"" = @envId AND t.""EventName"" = @eventName AND t.""Timestamp"" >= e.""FirstExposure""
                    GROUP BY 1, 2
                )
            SELECT 
                COALESCE(e.""TimeBucket"", c.""TimeBucket"") as ""TimeBucket"",
                COALESCE(e.""VariationId"", c.""VariationId"") as ""VariationId"",
                COALESCE(e.""Exposures"", 0) as ""Exposures"",
                COALESCE(c.""Conversions"", 0) as ""Conversions""
            FROM hourly_exposures e
            FULL OUTER JOIN hourly_conversions c 
              ON e.""TimeBucket"" = c.""TimeBucket"" AND e.""VariationId"" = c.""VariationId""
            ORDER BY 1, 2;
        ";

            var pEnv = command.CreateParameter(); pEnv.ParameterName = "@envId"; pEnv.Value = environmentId; command.Parameters.Add(pEnv);
            var pFlag = command.CreateParameter(); pFlag.ParameterName = "@flagKey"; pFlag.Value = flagKey; command.Parameters.Add(pFlag);
            var pEvent = command.CreateParameter(); pEvent.ParameterName = "@eventName"; pEvent.Value = eventName; command.Parameters.Add(pEvent);
            var pCutoff = command.CreateParameter(); pCutoff.ParameterName = "@cutoff"; pCutoff.Value = cutoff; command.Parameters.Add(pCutoff);

            var cumulativeExposures = new Dictionary<Guid, long>();
            var cumulativeConversions = new Dictionary<Guid, long>();

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var timeBucket = reader.GetDateTime(0);
                var variationId = reader.GetGuid(1);
                var exposures = reader.GetInt64(2);
                var conversions = reader.GetInt64(3);

                if (cumulativeExposures.TryAdd(variationId, 0))
                    cumulativeConversions[variationId] = 0;

                cumulativeExposures[variationId] += exposures;
                cumulativeConversions[variationId] += conversions;

                results.Add(new ExperimentTimeSeriesPoint(timeBucket, variationId, cumulativeExposures[variationId], cumulativeConversions[variationId]));
            }
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }

        return results;
    }
}
