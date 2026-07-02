using System.Globalization;
using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Infrastructure.Endpoints;
using AuthModels = ToggleMesh.API.Infrastructure.Security.Authorization.Models;

namespace ToggleMesh.API.Features.Analytics.Simulate;

public class SimulateExperimentEndpoint : ToggleEndpoint<SimulateExperimentRequest>
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SimulateExperimentEndpoint> _logger;
    private readonly IConfiguration _config;

    public SimulateExperimentEndpoint(IWebHostEnvironment env, ILogger<SimulateExperimentEndpoint> logger, IConfiguration config)
    {
        _env = env;
        _logger = logger;
        _config = config;
    }

    public override void Configure()
    {
        Post("/projects/{projectId}/environments/{environmentId}/flags/{flagKey}/experiments/simulate");
        Version(1);
        this.RequirePermission(AuthModels.Permissions.FlagsToggle);
    }

    public override async Task HandleAsync(SimulateExperimentRequest req, CancellationToken ct)
    {
        if (!_env.IsDevelopment())
        {
            _logger.LogWarning("Simulation endpoint was hit outside of Development environment. Denied.");
            await Send.NotFoundAsync(ct);
            return;
        }

        var environmentId = Route<Guid>("environmentId");
        var flagKey = Route<string>("flagKey");

        if (flagKey is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var storage = _config["Analytics:Storage"] ?? "PostgreSQL";

        await GenerateVariantDataAsync(environmentId, flagKey, req.EventName, 0, req.ParticipantsCount, req.ControlConversionRate, req.ControlValue, storage, ct);
        await GenerateVariantDataAsync(environmentId, flagKey, req.EventName, 1, req.ParticipantsCount, req.TreatmentConversionRate, req.TreatmentValue, storage, ct);

        await Send.OkAsync(new { Success = true, Message = $"Injected {req.ParticipantsCount * 2} users." }, ct);
    }

    private async Task GenerateVariantDataAsync(Guid environmentId, string flagKey, string eventName, int variant, int participants, double conversionRate, double? explicitValue, string storage, CancellationToken ct)
    {
        if (storage.Equals("ClickHouse", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = _config["Analytics:ClickHouse:ConnectionString"] ?? throw new InvalidOperationException("ClickHouse ConnectionString not found");

            var sql1 = $@"
                INSERT INTO AnalyticsExposures (Id, EnvironmentId, FlagKey, Identity, Variant, Timestamp)
                SELECT 
                    generateUUIDv4(), 
                    '{environmentId}', 
                    '{flagKey}', 
                    concat('sim-{variant}-', toString(number)), 
                    {(variant == 1 ? "1" : "0")}, 
                    now() - interval (rand() % 3600) second
                FROM numbers({participants})
            ";

            var sql2 = $@"
                INSERT INTO AnalyticsTracks (Id, EnvironmentId, Identity, EventName, Value, Properties, Timestamp)
                SELECT 
                    generateUUIDv4(), 
                    '{environmentId}', 
                    concat('sim-{variant}-', toString(number)), 
                    '{eventName}', 
                    {(explicitValue.HasValue ? explicitValue.Value.ToString(CultureInfo.InvariantCulture) : "(rand() % 45) + 5 + " + (variant == 1 ? "7.5" : "0.0"))}, 
                    concat('{{""Country"":""', (['US', 'CA', 'GB', 'AU'])[rand() % 4 + 1], '""}}'),
                    now() - interval (rand() % 3000) second
                FROM numbers({participants})
                WHERE rand() % 10000 < {(int)(conversionRate * 10000)}
            ";
            
            var sql3 = $@"
                INSERT INTO AnalyticsTracks (Id, EnvironmentId, Identity, EventName, Value, Properties, Timestamp)
                SELECT 
                    generateUUIDv4(), 
                    '{environmentId}', 
                    concat('sim-{variant}-', toString(number)), 
                    'identified', 
                    NULL, 
                    concat('{{""Country"":""', (['US', 'CA', 'GB', 'AU'])[rand() % 4 + 1], '""}}'),
                    now() - interval (rand() % 3600 + 1) second
                FROM numbers({participants})
            ";

            await using var connection = new ClickHouse.Client.ADO.ClickHouseConnection(connectionString);
            await connection.OpenAsync(ct);
            
            await using var command1 = connection.CreateCommand();
            command1.CommandText = sql1;
            await command1.ExecuteNonQueryAsync(ct);

            await using var command3 = connection.CreateCommand();
            command3.CommandText = sql3;
            await command3.ExecuteNonQueryAsync(ct);

            await using var command2 = connection.CreateCommand();
            command2.CommandText = sql2;
            await command2.ExecuteNonQueryAsync(ct);
        }
        else
        {
            using var scope = Resolve<IServiceScopeFactory>().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();

            var rand = new Random();
            var now = DateTime.UtcNow;
            
            var exposures = new List<AnalyticsExposure>();
            var tracks = new List<AnalyticsTrack>();

            for (var i = 0; i < participants; i++)
            {
                var identity = $"sim-{variant}-{i}";
                
                exposures.Add(new AnalyticsExposure
                {
                    Id = Guid.NewGuid(),
                    EnvironmentId = environmentId,
                    FlagKey = flagKey,
                    Identity = identity,
                    Variant = variant == 1,
                    Timestamp = now.AddSeconds(-rand.Next(0, 3600))
                });

                if (rand.Next(0, 10000) < (int)(conversionRate * 10000))
                {
                    var val = explicitValue ?? rand.Next(0, 45) + 5 + (variant == 1 ? 7.5 : 0.0);
                    var country = new[] { "US", "CA", "GB", "AU" }[rand.Next(0, 4)];
                    var props = global::System.Text.Json.JsonDocument.Parse($"{{\"Country\":\"{country}\"}}");

                    tracks.Add(new AnalyticsTrack
                    {
                        Id = Guid.NewGuid(),
                        EnvironmentId = environmentId,
                        Identity = identity,
                        EventName = eventName,
                        Value = (float)val,
                        Properties = props,
                        Timestamp = now.AddSeconds(-rand.Next(0, 3000))
                    });
                }
                
                var country2 = new[] { "US", "CA", "GB", "AU" }[rand.Next(0, 4)];
                var props2 = global::System.Text.Json.JsonDocument.Parse($"{{\"Country\":\"{country2}\"}}");
                tracks.Add(new AnalyticsTrack
                {
                    Id = Guid.NewGuid(),
                    EnvironmentId = environmentId,
                    Identity = identity,
                    EventName = "identified",
                    Value = null,
                    Properties = props2,
                    Timestamp = now.AddSeconds(-rand.Next(0, 3600) - 1)
                });
            }

            db.AnalyticsExposures.AddRange(exposures);
            db.AnalyticsTracks.AddRange(tracks);
            await db.SaveChangesAsync(ct);
        }
    }
}
