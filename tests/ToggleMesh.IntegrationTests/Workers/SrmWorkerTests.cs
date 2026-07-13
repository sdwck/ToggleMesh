using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Metrics.Workers;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;
using System.Reflection;

namespace ToggleMesh.IntegrationTests.Workers;

[Collection("SharedEnv1")]
public class SrmWorkerTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private AppDbContext _db = null!;
    private SrmWorker _worker = null!;
    private Channel<WebhookEvent> _webhookChannel = null!;

    public SrmWorkerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        var scope = _factory.Services.CreateScope();
        _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SrmWorker>>();
        _webhookChannel = scope.ServiceProvider.GetRequiredService<Channel<WebhookEvent>>();

        _worker = new SrmWorker(_factory.Services, logger, _factory.TimeProvider, _webhookChannel);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Worker_ShouldDetectSrmAndSendAlert()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var envId = Guid.CreateVersion7();
        var flagId = Guid.CreateVersion7();
        var varA = Guid.CreateVersion7();
        var varB = Guid.CreateVersion7();

        var project = new Project { Id = projectId, Name = "Test Project" };
        var env = new ProjectEnvironment { Id = envId, ProjectId = projectId, Name = "Test Env" };
        var flag = new FeatureFlag
        {
            Id = flagId,
            ProjectId = projectId,
            Key = "srm-flag",
            Name = "SRM Flag"
        };

        var state = new FlagEnvironmentState
        {
            Id = Guid.CreateVersion7(),
            FeatureFlagId = flagId,
            EnvironmentId = envId,
            IsExperimentActive = true,
            IsMabEnabled = false,
            IsSrmAlertSent = false,
            FallthroughRollout = new List<VariationWeight>
            {
                new VariationWeight { VariationId = varA, Weight = 5000 },
                new VariationWeight { VariationId = varB, Weight = 5000 }
            }
        };

        await _db.Projects.AddAsync(project);
        await _db.Environments.AddAsync(env);
        await _db.FeatureFlags.AddAsync(flag);
        await _db.FlagEnvironmentStates.AddAsync(state);
        
        var metricA = new ExperimentMetric
        {
            Id = Guid.CreateVersion7(),
            EnvironmentId = envId,
            FlagKey = "srm-flag",
            EventName = "$exposure",
            VariationId = varA,
            TotalExposures = 1500,
            TotalConversions = 0
        };

        var metricB = new ExperimentMetric
        {
            Id = Guid.CreateVersion7(),
            EnvironmentId = envId,
            FlagKey = "srm-flag",
            EventName = "$exposure",
            VariationId = varB,
            TotalExposures = 500,
            TotalConversions = 0
        };

        await _db.ExperimentMetrics.AddRangeAsync(metricA, metricB);
        await _db.SaveChangesAsync();

        while (_webhookChannel.Reader.TryRead(out _)) { }

        // Act
        var detectMethod = typeof(SrmWorker).GetMethod("DetectSrmAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)detectMethod!.Invoke(_worker, [CancellationToken.None])!;

        // Assert
        await _db.Entry(state).ReloadAsync();
        state.IsSrmAlertSent.Should().BeTrue();
        state.SrmPValue.Should().NotBeNull();
        state.SrmPValue.Should().BeLessThan(0.001);

        var hasWebhook = _webhookChannel.Reader.TryRead(out var ev);
        hasWebhook.Should().BeTrue();
        ev!.EventName.Should().Be("experiment.srm_detected");
    }
    
    [Fact]
    public async Task Worker_ShouldNotDetectSrmWhenTrafficIsBalanced()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var envId = Guid.CreateVersion7();
        var flagId = Guid.CreateVersion7();
        var varA = Guid.CreateVersion7();
        var varB = Guid.CreateVersion7();

        var project = new Project { Id = projectId, Name = "Test Project" };
        var env = new ProjectEnvironment { Id = envId, ProjectId = projectId, Name = "Test Env" };
        var flag = new FeatureFlag
        {
            Id = flagId,
            ProjectId = projectId,
            Key = "srm-flag-balanced",
            Name = "SRM Flag Balanced"
        };

        var state = new FlagEnvironmentState
        {
            Id = Guid.CreateVersion7(),
            FeatureFlagId = flagId,
            EnvironmentId = envId,
            IsExperimentActive = true,
            IsMabEnabled = false,
            IsSrmAlertSent = false,
            FallthroughRollout = new List<VariationWeight>
            {
                new VariationWeight { VariationId = varA, Weight = 5000 },
                new VariationWeight { VariationId = varB, Weight = 5000 }
            }
        };

        await _db.Projects.AddAsync(project);
        await _db.Environments.AddAsync(env);
        await _db.FeatureFlags.AddAsync(flag);
        await _db.FlagEnvironmentStates.AddAsync(state);
        
        var metricA = new ExperimentMetric
        {
            Id = Guid.CreateVersion7(),
            EnvironmentId = envId,
            FlagKey = "srm-flag-balanced",
            EventName = "$exposure",
            VariationId = varA,
            TotalExposures = 1010,
            TotalConversions = 0
        };

        var metricB = new ExperimentMetric
        {
            Id = Guid.CreateVersion7(),
            EnvironmentId = envId,
            FlagKey = "srm-flag-balanced",
            EventName = "$exposure",
            VariationId = varB,
            TotalExposures = 990,
            TotalConversions = 0
        };

        await _db.ExperimentMetrics.AddRangeAsync(metricA, metricB);
        await _db.SaveChangesAsync();
        while (_webhookChannel.Reader.TryRead(out _)) { }

        // Act
        var detectMethod = typeof(SrmWorker).GetMethod("DetectSrmAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)detectMethod!.Invoke(_worker, [CancellationToken.None])!;

        // Assert
        await _db.Entry(state).ReloadAsync();
        state.IsSrmAlertSent.Should().BeFalse();
        state.SrmPValue.Should().BeNull();

        var hasWebhook = _webhookChannel.Reader.TryRead(out _);
        hasWebhook.Should().BeFalse();
    }
}
