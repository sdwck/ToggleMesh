using System.Threading.Channels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Metrics.Domain;
using ToggleMesh.API.Features.Metrics.Workers;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Workers;

[Collection("SharedEnv4")]
public class MetricsWorkerTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private AppDbContext _db = null!;
    private MetricsWorker _worker = null!;
    private Channel<MetricQueueItem> _channel = null!;

    public MetricsWorkerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        var scope = _factory.Services.CreateScope();
        _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MetricsWorker>>();
        _channel = scope.ServiceProvider.GetRequiredService<Channel<MetricQueueItem>>();
        var configuration = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        _worker = new MetricsWorker(_channel, _factory.Services, logger, _factory.TimeProvider, configuration);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Worker_ShouldAggregateMetricsAndSaveToDatabase()
    {
        // Arrange
        var envId = Guid.CreateVersion7();
        var flagKey = "test-flag";

        var project = new Project { Id = Guid.CreateVersion7(), Name = "P" };
        var env = new ProjectEnvironment { Id = envId, ProjectId = project.Id, Name = "E" };
        var flag = new FeatureFlag { Id = Guid.CreateVersion7(), ProjectId = project.Id, Key = flagKey, IsClientSideExposed = true };
        var state = new FlagEnvironmentState { Id = Guid.CreateVersion7(), FeatureFlagId = flag.Id, EnvironmentId = envId };

        await _db.Projects.AddAsync(project);
        await _db.Environments.AddAsync(env);
        await _db.FeatureFlags.AddAsync(flag);
        await _db.FlagEnvironmentStates.AddAsync(state);
        await _db.SaveChangesAsync();

        var metricEvent1 = new MetricQueueItem(envId, flagKey, true, 1, 0);
        var metricEvent2 = new MetricQueueItem(envId, flagKey, true, 0, 1);

        await _channel.Writer.WriteAsync(metricEvent1);
        await _channel.Writer.WriteAsync(metricEvent2);

        // Act
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(35));
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task = _worker.StartAsync(cts.Token);
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(6));
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }

        // Assert
        var buckets = await _db.FlagMetricBuckets.ToListAsync(cancellationToken: cts.Token);
        buckets.Should().NotBeEmpty();

        var targetBucket = buckets.FirstOrDefault(b => b.FlagKey == flagKey);
        targetBucket.Should().NotBeNull();
        targetBucket.TrueCount.Should().BeGreaterThanOrEqualTo(1);
        targetBucket.FalseCount.Should().BeGreaterThanOrEqualTo(1);
    }
}
