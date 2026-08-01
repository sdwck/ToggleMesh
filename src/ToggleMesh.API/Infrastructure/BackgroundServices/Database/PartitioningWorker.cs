using ClickHouse.Client.ADO;
using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;
using Quartz;

namespace ToggleMesh.API.Infrastructure.BackgroundServices.Database;

[DisallowConcurrentExecution]
public class PartitioningWorker : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PartitioningWorker> _logger;

    public PartitioningWorker(IServiceProvider serviceProvider, ILogger<PartitioningWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("PartitioningWorker executing job {JobKey}", context.JobDetail.Key);

        try
        {
            var stoppingToken = context.CancellationToken;
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var retentionDays = configuration.GetValue("AuditLogs:RetentionDays", 0);
            if (retentionDays > 0)
            {
                var thresholdDate = DateTime.UtcNow.AddDays(-retentionDays);
                await db.Database.ExecuteSqlAsync($@"
                    DELETE FROM ""AuditLogs"" 
                    WHERE ""Timestamp"" < {thresholdDate};
                ", stoppingToken);
                _logger.LogInformation("Cleaned up AuditLogs older than {RetentionDays} days", retentionDays);
            }

            var analyticsRetentionDays = configuration.GetValue("Analytics:RetentionDays", 90);
            if (analyticsRetentionDays > 0)
            {
                var analyticsThreshold = DateTimeOffset.UtcNow.AddDays(-analyticsRetentionDays);
                var exposuresDeleted = await db.AnalyticsExposures
                    .Where(e => e.Timestamp < analyticsThreshold)
                    .ExecuteDeleteAsync(stoppingToken);
                var tracksDeleted = await db.AnalyticsTracks
                    .Where(t => t.Timestamp < analyticsThreshold)
                    .ExecuteDeleteAsync(stoppingToken);

                if (exposuresDeleted > 0 || tracksDeleted > 0)
                {
                    _logger.LogInformation("Cleaned up raw analytics older than {Days} days (exposures: {Exposures}, tracks: {Tracks})", 
                        analyticsRetentionDays, exposuresDeleted, tracksDeleted);
                }

                var chConnectionString = configuration["Analytics:ClickHouse:ConnectionString"];
                if (!string.IsNullOrWhiteSpace(chConnectionString))
                {
                    try
                    {
                        await using var chConn = new ClickHouseConnection(chConnectionString);
                        await chConn.OpenAsync(stoppingToken);

                        var cutoffStr = analyticsThreshold.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                        await using (var cmd1 = chConn.CreateCommand())
                        {
                            cmd1.CommandText = "ALTER TABLE AnalyticsExposures DELETE WHERE Timestamp < {cutoff:String}";
                            var pCutoff = cmd1.CreateParameter(); pCutoff.ParameterName = "cutoff"; pCutoff.Value = cutoffStr; cmd1.Parameters.Add(pCutoff);
                            await cmd1.ExecuteNonQueryAsync(stoppingToken);
                        }

                        await using (var cmd2 = chConn.CreateCommand())
                        {
                            cmd2.CommandText = "ALTER TABLE AnalyticsTracks DELETE WHERE Timestamp < {cutoff:String}";
                            var pCutoff = cmd2.CreateParameter(); pCutoff.ParameterName = "cutoff"; pCutoff.Value = cutoffStr; cmd2.Parameters.Add(pCutoff);
                            await cmd2.ExecuteNonQueryAsync(stoppingToken);
                        }

                        _logger.LogInformation("Cleaned up ClickHouse analytics records older than {Days} days", analyticsRetentionDays);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to execute ClickHouse analytics retention cleanup");
                    }
                }
            }

            var nextMonth = DateTime.UtcNow.AddMonths(1);
            var startDate = new DateTime(nextMonth.Year, nextMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);
            var partitionName = $"AuditLogs_{startDate:yyyy_MM}";

            var sql = $@"
                CREATE TABLE IF NOT EXISTS ""{partitionName}"" 
                PARTITION OF ""AuditLogs"" 
                FOR VALUES FROM ('{startDate:yyyy-MM-dd}') TO ('{endDate:yyyy-MM-dd}');
            ";

            await db.Database.ExecuteSqlRawAsync(sql, stoppingToken);
            _logger.LogInformation("Ensured partition {PartitionName} exists for AuditLogs", partitionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during partitioning and cleanup.");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
