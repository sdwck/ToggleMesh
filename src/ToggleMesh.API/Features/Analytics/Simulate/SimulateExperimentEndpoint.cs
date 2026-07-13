using ToggleMesh.API.Extensions;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Endpoints;
using ToggleMesh.API.Features.Analytics.Ingest;
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

        var clickHouseConn = _config["Analytics:ClickHouse:ConnectionString"];
        var storage = string.IsNullOrWhiteSpace(clickHouseConn) ? "PostgreSQL" : "ClickHouse";
            
        if (req.Variations.Count == 0)
            ThrowError("Variations are required");

        foreach (var v in req.Variations)
            await GenerateVariantDataAsync(environmentId, flagKey, req.EventName, v.VariationId, req.ParticipantsCount, v.ConversionRate, v.Value, req.ContextProperties, storage, ct);

        var queryEngine = Resolve<IAnalyticsQueryEngine>();
        await queryEngine.AggregateMetricsAsync(ct);
        await queryEngine.AggregateContextualMetricsAsync(ct);

        await Send.OkAsync(new
        {
            Success = true, 
            Message = $"Injected {req.ParticipantsCount * req.Variations.Count} users and triggered aggregation."
        }, ct);
    }

    private async Task GenerateVariantDataAsync(Guid environmentId, string flagKey, string eventName, Guid variationId, int participants, double conversionRate, double? explicitValue, Dictionary<string, string[]> contextProperties, string storage, CancellationToken ct)
    {
        if (storage.Equals("ClickHouse", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = _config["Analytics:ClickHouse:ConnectionString"] ?? throw new InvalidOperationException("ClickHouse ConnectionString not found");

            string chPropsStr;
            if (contextProperties is { Count: > 0 })
            {
                var dictItems = contextProperties
                    .Select(kvp => 
                        $"'{kvp.Key}':" + $"(['" + string.Join("','", kvp.Value.Select(v => v.Replace("'", "''"))) + $"'])[rand() % {kvp.Value.Length} + 1]").ToList();
                chPropsStr = "concat('{{'," + string.Join(",',',", dictItems) + ",'}}')";
            }
            else
                chPropsStr = "concat('{{\"country\":\"', (['US', 'CA', 'GB', 'AU'])[rand() % 4 + 1], '\"}}')";

            var sql1 = $@"
                INSERT INTO AnalyticsExposures (Id, EnvironmentId, FlagKey, Identity, VariationId, Timestamp, Properties)
                SELECT 
                    generateUUIDv4(), 
                    '{environmentId}', 
                    '{flagKey}', 
                    concat('sim-{variationId}-', toString(number)), 
                    '{variationId}', 
                    now() - interval (rand() % 3600) second,
                    {chPropsStr}
                FROM numbers({participants})
            ";

            var sql2 = $@"
                INSERT INTO AnalyticsTracks (Id, EnvironmentId, Identity, EventName, Value, Properties, Timestamp)
                SELECT 
                    generateUUIDv4(), 
                    '{environmentId}', 
                    e.Identity, 
                    '{eventName}', 
                    {(explicitValue.HasValue ? explicitValue.Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture) : "(rand() % 45) + 5")}, 
                    e.Properties,
                    e.Timestamp + interval (rand() % 600 + 1) second
                FROM AnalyticsExposures e
                WHERE e.EnvironmentId = '{environmentId}' AND e.VariationId = '{variationId}'
                  AND rand() % 10000 < {(int)(conversionRate * 10000)}
            ";
            
            var sql3 = $@"
                INSERT INTO AnalyticsTracks (Id, EnvironmentId, Identity, EventName, Value, Properties, Timestamp)
                SELECT 
                    generateUUIDv4(), 
                    '{environmentId}', 
                    e.Identity, 
                    'identified', 
                    NULL, 
                    e.Properties,
                    e.Timestamp - interval (rand() % 10 + 1) second
                FROM AnalyticsExposures e
                WHERE e.EnvironmentId = '{environmentId}' AND e.VariationId = '{variationId}'
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
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var rand = new Random();
            var now = DateTime.UtcNow;
            
            var exposures = new List<AnalyticsExposure>();
            var tracks = new List<AnalyticsTrack>();

            for (var i = 0; i < participants; i++)
            {
                if (i % 1000 == 0) 
                    ct.ThrowIfCancellationRequested();

                var identity = $"sim-{variationId}-{i}";

                var propsDict = new Dictionary<string, string>();
                if (contextProperties is { Count: > 0 })
                {
                    foreach (var kvp in contextProperties)
                        if (kvp.Value is { Length: > 0 })
                            propsDict[kvp.Key] = kvp.Value[rand.Next(0, kvp.Value.Length)];
                }
                else
                    propsDict["country"] = new[] { "US", "CA", "GB", "AU" }[rand.Next(0, 4)];
                var jsonProps = global::System.Text.Json.JsonSerializer.Serialize(propsDict);
                var props = global::System.Text.Json.JsonDocument.Parse(jsonProps);
                
                var exposureTime = now.AddSeconds(-rand.Next(0, 3600));
                
                exposures.Add(new AnalyticsExposure
                {
                    Id = Guid.NewGuid(),
                    EnvironmentId = environmentId,
                    FlagKey = flagKey,
                    Identity = identity,
                    VariationId = variationId,
                    Timestamp = exposureTime,
                    Properties = props
                });

                if (rand.Next(0, 10000) < (int)(conversionRate * 10000))
                {
                    var val = explicitValue ?? rand.Next(0, 45) + 5;
                    tracks.Add(new AnalyticsTrack
                    {
                        Id = Guid.NewGuid(),
                        EnvironmentId = environmentId,
                        Identity = identity,
                        EventName = eventName,
                        Value = (float)val,
                        Properties = props,
                        Timestamp = exposureTime.AddSeconds(rand.Next(1, 600))
                    });
                }
                
                tracks.Add(new AnalyticsTrack
                {
                    Id = Guid.NewGuid(),
                    EnvironmentId = environmentId,
                    Identity = identity,
                    EventName = "identified",
                    Value = null,
                    Properties = props,
                    Timestamp = exposureTime.AddSeconds(-rand.Next(1, 10))
                });
            }

            db.AnalyticsExposures.AddRange(exposures);
            db.AnalyticsTracks.AddRange(tracks);
            await db.SaveChangesAsync(ct);
        }
    }
}
