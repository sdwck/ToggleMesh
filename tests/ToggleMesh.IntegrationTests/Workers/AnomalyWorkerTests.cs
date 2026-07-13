using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Metrics.Workers;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Workers;

[Collection("SharedEnv4")]
public class AnomalyWorkerTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private AppDbContext _db = null!;
    private AnomalyWorker _worker = null!;

    public AnomalyWorkerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        var scope = _factory.Services.CreateScope();
        _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AnomalyWorker>>();
        var webhookChannel = scope.ServiceProvider.GetRequiredService<Channel<WebhookEvent>>();
        var mathService = scope.ServiceProvider.GetRequiredService<BayesianMathService>();

        _worker = new AnomalyWorker(_factory.Services, logger, _factory.TimeProvider, webhookChannel, mathService);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Worker_ShouldDetectAnomalyAndSendAlert()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var envId = Guid.CreateVersion7();
        var flagId = Guid.CreateVersion7();

        var project = new Project { Id = projectId, Name = "Test Project" };
        var env = new ProjectEnvironment { Id = envId, ProjectId = projectId, Name = "Test Env" };
        var flag = new FeatureFlag
        {
            Id = flagId,
            ProjectId = projectId,
            Key = "test-flag",
            Name = "Test Flag"
        };

        var state = new FlagEnvironmentState
        {
            Id = Guid.CreateVersion7(),
            FeatureFlagId = flagId,
            EnvironmentId = envId,
            IsExperimentActive = true,
            MabGoalEvent = "checkout",
            OffVariationId = Guid.Empty
        };

        await _db.Projects.AddAsync(project);
        await _db.Environments.AddAsync(env);
        await _db.FeatureFlags.AddAsync(flag);
        await _db.FlagEnvironmentStates.AddAsync(state);
        
        var metricControl = new ExperimentMetric
        {
            Id = Guid.CreateVersion7(),
            EnvironmentId = envId,
            FlagKey = "test-flag",
            EventName = "checkout",
            VariationId = Guid.Empty,
            TotalExposures = 1000,
            TotalConversions = 200
        };

        var metricTreatment = new ExperimentMetric
        {
            Id = Guid.CreateVersion7(),
            EnvironmentId = envId,
            FlagKey = "test-flag",
            EventName = "checkout",
            VariationId = Guid.NewGuid(),
            TotalExposures = 1000,
            TotalConversions = 10
        };

        await _db.ExperimentMetrics.AddRangeAsync(metricControl, metricTreatment);
        await _db.SaveChangesAsync();

        // Act
        _factory.TimeProvider.Advance(TimeSpan.FromMinutes(65));

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var method = typeof(AnomalyWorker).GetMethod("DetectAnomaliesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(_worker, [cts.Token])!;

        // Assert
        await _db.Entry(metricTreatment).ReloadAsync(cts.Token);
        var updatedTreatment = metricTreatment;
        updatedTreatment.Should().NotBeNull();
        updatedTreatment.IsAlertSent.Should().BeTrue();
    }
}
