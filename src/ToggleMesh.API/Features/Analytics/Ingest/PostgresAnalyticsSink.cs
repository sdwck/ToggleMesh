using Npgsql;
using ToggleMesh.API.Features.Analytics.Domain;

namespace ToggleMesh.API.Features.Analytics.Ingest;

public class PostgresAnalyticsSink : IAnalyticsStorageSink
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresAnalyticsSink> _logger;

    public PostgresAnalyticsSink(IConfiguration configuration, ILogger<PostgresAnalyticsSink> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection not found");
        _logger = logger;
    }

    public async Task WriteBatchAsync(List<AnalyticsExposure> exposures, List<AnalyticsTrack> tracks, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        if (exposures.Count > 0)
        {
            await using var writer = await conn.BeginBinaryImportAsync("COPY \"AnalyticsExposures\" (\"Id\", \"EnvironmentId\", \"FlagKey\", \"Identity\", \"VariationId\", \"Properties\", \"Timestamp\") FROM STDIN (FORMAT BINARY)", ct);
            foreach (var e in exposures)
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(e.Id, NpgsqlTypes.NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(e.EnvironmentId, NpgsqlTypes.NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(e.FlagKey, NpgsqlTypes.NpgsqlDbType.Text, ct);
                await writer.WriteAsync(e.Identity, NpgsqlTypes.NpgsqlDbType.Text, ct);
                await writer.WriteAsync(e.VariationId, NpgsqlTypes.NpgsqlDbType.Uuid, ct);
                
                if (e.Properties != null)
                    await writer.WriteAsync(e.Properties, NpgsqlTypes.NpgsqlDbType.Jsonb, ct);
                else
                    await writer.WriteNullAsync(ct);
                    
                await writer.WriteAsync(e.Timestamp, NpgsqlTypes.NpgsqlDbType.TimestampTz, ct);
            }
            await writer.CompleteAsync(ct);
        }

        if (tracks.Count > 0)
        {
            await using var writer = await conn.BeginBinaryImportAsync("COPY \"AnalyticsTracks\" (\"Id\", \"EnvironmentId\", \"Identity\", \"EventName\", \"Value\", \"Properties\", \"Timestamp\") FROM STDIN (FORMAT BINARY)", ct);
            foreach (var t in tracks)
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(t.Id, NpgsqlTypes.NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(t.EnvironmentId, NpgsqlTypes.NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(t.Identity, NpgsqlTypes.NpgsqlDbType.Text, ct);
                await writer.WriteAsync(t.EventName, NpgsqlTypes.NpgsqlDbType.Text, ct);
                
                if (t.Value.HasValue)
                    await writer.WriteAsync(t.Value.Value, NpgsqlTypes.NpgsqlDbType.Double, ct);
                else
                    await writer.WriteNullAsync(ct);

                if (t.Properties != null)
                    await writer.WriteAsync(t.Properties, NpgsqlTypes.NpgsqlDbType.Jsonb, ct);
                else
                    await writer.WriteNullAsync(ct);

                await writer.WriteAsync(t.Timestamp, NpgsqlTypes.NpgsqlDbType.TimestampTz, ct);
            }
            await writer.CompleteAsync(ct);
        }
        
        _logger.LogInformation("[PostgresAnalyticsSink] Flushed {ExposureCount} exposures and {TrackCount} tracks using COPY.", exposures.Count, tracks.Count);
    }
}
