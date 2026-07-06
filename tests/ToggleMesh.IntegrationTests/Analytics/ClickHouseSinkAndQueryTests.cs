using ClickHouse.Client.ADO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.ClickHouse;
using Testcontainers.PostgreSql;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;

namespace ToggleMesh.IntegrationTests.Analytics;

public class ClickHouseSinkAndQueryTests : IAsyncLifetime
{
    private readonly ClickHouseContainer _clickHouseContainer = new ClickHouseBuilder(
            "clickhouse/clickhouse-server:24.3")
        .Build();

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder(
            "postgres:15-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_clickHouseContainer.StartAsync(), _postgresContainer.StartAsync());
        await SetupClickHouseSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_clickHouseContainer.DisposeAsync().AsTask(), _postgresContainer.DisposeAsync().AsTask());
    }

    private async Task SetupClickHouseSchemaAsync()
    {
        await using var connection = new ClickHouseConnection(_clickHouseContainer.GetConnectionString());
        await connection.OpenAsync();

        var createExposures = @"
            CREATE TABLE AnalyticsExposures (
                Id UUID,
                EnvironmentId UUID,
                FlagKey String,
                Identity String,
                Variant UInt8,
                Properties String,
                Timestamp DateTime
            ) ENGINE = MergeTree()
            ORDER BY (EnvironmentId, FlagKey, Timestamp)";

        await using var cmd1 = connection.CreateCommand();
        cmd1.CommandText = createExposures;
        await cmd1.ExecuteNonQueryAsync();

        var createTracks = @"
            CREATE TABLE AnalyticsTracks (
                Id UUID,
                EnvironmentId UUID,
                Identity String,
                EventName String,
                Value Nullable(Float64),
                Properties String,
                Timestamp DateTime
            ) ENGINE = MergeTree()
            ORDER BY (EnvironmentId, EventName, Timestamp)";

        await using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = createTracks;
        await cmd2.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task SinkAndQuery_ShouldAggregateCorrectly_AndSaveToPostgres()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"Analytics:ClickHouse:ConnectionString", _clickHouseContainer.GetConnectionString()}
        });
        var configuration = configBuilder.Build();

        var sink = new ClickHouseAnalyticsSink(configuration, NullLogger<ClickHouseAnalyticsSink>.Instance);

        var environmentId = Guid.NewGuid();
        var exposures = new List<AnalyticsExposure>
        {
            new() { Id = Guid.NewGuid(), EnvironmentId = environmentId, FlagKey = "flag-1", Identity = "user-1", Variant = true, Timestamp = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), EnvironmentId = environmentId, FlagKey = "flag-1", Identity = "user-2", Variant = false, Timestamp = DateTimeOffset.UtcNow }
        };

        var tracks = new List<AnalyticsTrack>
        {
            new() { Id = Guid.NewGuid(), EnvironmentId = environmentId, Identity = "user-1", EventName = "checkout", Timestamp = DateTimeOffset.UtcNow.AddMinutes(1) }
        };

        await sink.WriteBatchAsync(exposures, tracks);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;
        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var org = new Organization { Name = "Test Org" };
        var project = new Project { Name = "Test", Organization = org };
        var environment = new ProjectEnvironment { Id = environmentId, Name = "Test Env", Project = project };
        var flag = new FeatureFlag { Key = "flag-1", Project = project };
        var state = new FlagEnvironmentState { Environment = environment, FeatureFlag = flag, RolloutPercentage = 50, IsEnabled = true };

        db.Organizations.Add(org);
        db.Projects.Add(project);
        db.Environments.Add(environment);
        db.FeatureFlags.Add(flag);
        db.FlagEnvironmentStates.Add(state);
        await db.SaveChangesAsync();

        var queryEngine = new ClickHouseQueryEngine(configuration, db, NullLogger<ClickHouseQueryEngine>.Instance);
        await queryEngine.AggregateMetricsAsync(CancellationToken.None);

        var metrics = await db.ExperimentMetrics.ToListAsync();
        metrics.Should().HaveCount(2);

        var treatmentMetric = metrics.First(x => x.Variant);
        treatmentMetric.TotalExposures.Should().Be(1);
        treatmentMetric.TotalConversions.Should().Be(1);

        var controlMetric = metrics.First(x => !x.Variant);
        controlMetric.TotalExposures.Should().Be(1);
        controlMetric.TotalConversions.Should().Be(0);
    }
}
