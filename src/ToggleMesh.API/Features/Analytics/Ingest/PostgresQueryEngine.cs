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
    }

    public async Task AggregateMetricsAsync(CancellationToken ct = default)
    {
        var sql = @"
            WITH active_rollouts AS (
                SELECT fes.""EnvironmentId"", ff.""Key"" as ""FlagKey""
                FROM ""FlagEnvironmentStates"" fes
                JOIN ""ProjectFeatureFlags"" ff ON ff.""Id"" = fes.""FeatureFlagId""
            ),
            exposed_users AS (
                SELECT 
                    e.""EnvironmentId"", 
                    e.""FlagKey"", 
                    e.""Identity"", 
                    e.""Variant"", 
                    MIN(e.""Timestamp"") as ""FirstExposureTimestamp""
                FROM ""AnalyticsExposures"" e
                JOIN active_rollouts ar 
                  ON e.""EnvironmentId"" = ar.""EnvironmentId"" 
                 AND e.""FlagKey"" = ar.""FlagKey""
                GROUP BY e.""EnvironmentId"", e.""FlagKey"", e.""Identity"", e.""Variant""
            ),
            conversions AS (
                SELECT
                    e.""EnvironmentId"",
                    e.""FlagKey"",
                    t.""EventName"",
                    e.""Variant"",
                    COUNT(DISTINCT e.""Identity"") as ""TotalConversions"",
                    SUM(COALESCE(t.""Value"", 0)) as ""TotalValue"",
                    SUM(COALESCE(t.""Value"", 0) * COALESCE(t.""Value"", 0)) as ""SumOfSquaredValues""
                FROM exposed_users e
                JOIN ""AnalyticsTracks"" t 
                  ON e.""EnvironmentId"" = t.""EnvironmentId"" 
                 AND e.""Identity"" = t.""Identity""
                 AND t.""Timestamp"" >= e.""FirstExposureTimestamp""
                GROUP BY e.""EnvironmentId"", e.""FlagKey"", t.""EventName"", e.""Variant""
            ),
            exposures_count AS (
                SELECT 
                    ""EnvironmentId"",
                    ""FlagKey"",
                    ""Variant"",
                    COUNT(DISTINCT ""Identity"") as ""TotalExposures""
                FROM exposed_users
                GROUP BY ""EnvironmentId"", ""FlagKey"", ""Variant""
            )
            INSERT INTO ""ExperimentMetrics"" (""Id"", ""EnvironmentId"", ""FlagKey"", ""EventName"", ""Variant"", ""TotalExposures"", ""TotalConversions"", ""TotalValue"", ""SumOfSquaredValues"", ""LastCalculatedAt"", ""CreatedAt"")
            SELECT 
                gen_random_uuid(),
                e.""EnvironmentId"",
                e.""FlagKey"",
                c.""EventName"",
                e.""Variant"",
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
             AND e.""Variant"" = c.""Variant""
            ON CONFLICT (""EnvironmentId"", ""FlagKey"", ""EventName"", ""Variant"") 
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
            .Where(f => f.RolloutPercentage != null && f.ContextPartitionKeys != null && f.ContextPartitionKeys.Length > 0)
            .AsSplitQuery()
            .ToListAsync(ct);

        if (activeRollouts.Count == 0) return;

        foreach (var state in activeRollouts)
        {
            var keys = state.ContextPartitionKeys;

            var exposures = await _db.AnalyticsExposures
                .AsNoTracking()
                .Where(e => e.EnvironmentId == state.EnvironmentId && e.FlagKey == state.FeatureFlag.Key)
                .ToListAsync(ct);
            var allExposedIdentities = exposures.Select(e => e.Identity).ToHashSet();
                
            var tracks = await _db.AnalyticsTracks
                .AsNoTracking()
                .Where(t => t.EnvironmentId == state.EnvironmentId && (t.EventName == state.MabGoalEvent || t.Properties != null))
                .Where(t => allExposedIdentities.Contains(t.Identity))
                .ToListAsync(ct);

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
    }

    private string GetContextSliceString(JsonDocument? properties, string[] keys)
    {
        if (properties == null) return "{}";
        var dict = new Dictionary<string, string>();
        foreach (var key in keys)
        {
            if (properties.RootElement.TryGetProperty(key, out var el))
                dict[key] = el.ToString();
            else
                dict[key] = "null";
        }
        return JsonSerializer.Serialize(dict);
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
                Count = g.Sum(x => x.TrueCount + x.FalseCount)
            })
            .OrderBy(x => x.Time)
            .ToListAsync(ct);

        return query.Select(x => (x.Time, x.Count));
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
                        ""Variant"",
                        COUNT(DISTINCT ""Identity"") as ""Exposures""
                    FROM ""AnalyticsExposures""
                    WHERE ""EnvironmentId"" = @envId AND ""FlagKey"" = @flagKey AND ""Timestamp"" >= @cutoff
                    GROUP BY 1, 2
                ),
                exposed_users AS (
                    SELECT ""Identity"", ""Variant"", MIN(""Timestamp"") as ""FirstExposure""
                    FROM ""AnalyticsExposures""
                    WHERE ""EnvironmentId"" = @envId AND ""FlagKey"" = @flagKey AND ""Timestamp"" >= @cutoff
                    GROUP BY 1, 2
                ),
                hourly_conversions AS (
                    SELECT 
                        DATE_TRUNC('minute', t.""Timestamp"") as ""TimeBucket"",
                        e.""Variant"",
                        COUNT(DISTINCT t.""Identity"") as ""Conversions""
                    FROM ""AnalyticsTracks"" t
                    JOIN exposed_users e ON t.""Identity"" = e.""Identity""
                    WHERE t.""EnvironmentId"" = @envId AND t.""EventName"" = @eventName AND t.""Timestamp"" >= e.""FirstExposure""
                    GROUP BY 1, 2
                )
                SELECT 
                    COALESCE(e.""TimeBucket"", c.""TimeBucket"") as ""TimeBucket"",
                    COALESCE(e.""Variant"", c.""Variant"") as ""Variant"",
                    COALESCE(e.""Exposures"", 0) as ""Exposures"",
                    COALESCE(c.""Conversions"", 0) as ""Conversions""
                FROM hourly_exposures e
                FULL OUTER JOIN hourly_conversions c 
                  ON e.""TimeBucket"" = c.""TimeBucket"" AND e.""Variant"" = c.""Variant""
                ORDER BY 1, 2;
            ";

            var pEnv = command.CreateParameter(); pEnv.ParameterName = "@envId"; pEnv.Value = environmentId; command.Parameters.Add(pEnv);
            var pFlag = command.CreateParameter(); pFlag.ParameterName = "@flagKey"; pFlag.Value = flagKey; command.Parameters.Add(pFlag);
            var pEvent = command.CreateParameter(); pEvent.ParameterName = "@eventName"; pEvent.Value = eventName; command.Parameters.Add(pEvent);
            var pCutoff = command.CreateParameter(); pCutoff.ParameterName = "@cutoff"; pCutoff.Value = cutoff; command.Parameters.Add(pCutoff);

            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var timeBucket = reader.GetDateTime(0);
                var variant = reader.GetBoolean(1);
                var exposures = reader.GetInt64(2);
                var conversions = reader.GetInt64(3);
                results.Add(new ExperimentTimeSeriesPoint(timeBucket, variant, exposures, conversions));
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
