using Microsoft.EntityFrameworkCore;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.API.Infrastructure.BackgroundServices.Database;

public class PartitioningWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PartitioningWorker> _logger;

    public PartitioningWorker(IServiceProvider serviceProvider, ILogger<PartitioningWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to create next month's partition for AuditLogs");
            }

            try
            {
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
