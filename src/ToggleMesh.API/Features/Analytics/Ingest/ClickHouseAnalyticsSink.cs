using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using ToggleMesh.API.Features.Analytics.Domain;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class ClickHouseAnalyticsSink : IAnalyticsStorageSink
{
    private readonly string _connectionString;
    private readonly ILogger<ClickHouseAnalyticsSink> _logger;

    public ClickHouseAnalyticsSink(IConfiguration configuration, ILogger<ClickHouseAnalyticsSink> logger)
    {
        _connectionString = configuration["Analytics:ClickHouse:ConnectionString"] 
            ?? throw new InvalidOperationException("ClickHouse ConnectionString not found in configuration");
        _logger = logger;
    }

    public async Task WriteBatchAsync(List<AnalyticsExposure> exposures, List<AnalyticsTrack> tracks, CancellationToken ct = default)
    {
        await using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(ct);

        if (exposures.Count > 0)
        {
            using var bulkCopy = new ClickHouseBulkCopy(connection)
            {
                DestinationTableName = "AnalyticsExposures",
                BatchSize = 100000
            };
            
            await bulkCopy.InitAsync();
            
            var rows = exposures.Select(e => new[]
            {
                e.Id,
                e.EnvironmentId,
                e.FlagKey,
                e.Identity,
                e.VariationId,
                e.Properties?.RootElement.ToString() ?? (object)string.Empty,
                e.Timestamp.UtcDateTime
            }).ToArray();

            await bulkCopy.WriteToServerAsync(rows, ct);
        }

        if (tracks.Count > 0)
        {
            using var bulkCopy = new ClickHouseBulkCopy(connection)
            {
                DestinationTableName = "AnalyticsTracks",
                BatchSize = 100000
            };
            
            await bulkCopy.InitAsync();
            
            var rows = tracks.Select(t => new[]
            {
                t.Id,
                t.EnvironmentId,
                t.Identity,
                t.EventName,
                t.Value ?? (object)DBNull.Value,
                t.Properties?.RootElement.ToString() ?? (object)string.Empty,
                t.Timestamp.UtcDateTime
            }).ToArray();

            await bulkCopy.WriteToServerAsync(rows, ct);
        }

        _logger.LogInformation("[ClickHouseAnalyticsSink] Flushed {ExposureCount} exposures and {TrackCount} tracks using ClickHouseBulkCopy.", exposures.Count, tracks.Count);
    }
}
