using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Analytics.Simulate;
using ToggleMesh.API.Features.Flags.Commands;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Caching;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Streaming;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Analytics;

[Collection("SharedEnv4")]
public class MabEndToEndTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public MabEndToEndTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid EnvironmentId, string FlagKey, Guid ControlId, Guid TreatmentId)> SeedDataAsync(string flagKey, bool isMabEnabled, string[]? contextPartitionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var testOrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var project = new Project { Name = "MAB E2E Project", OrganizationId = testOrgId };
        db.Projects.Add(project);

        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Role = ProjectRole.Owner
        });

        var environment = new ProjectEnvironment { Name = "Production", Project = project };
        db.Environments.Add(environment);

        var controlId = Guid.NewGuid();
        var treatmentId = Guid.NewGuid();

        var flag = new FeatureFlag 
        { 
            Project = project, 
            Key = flagKey,
            Variations = new List<FlagVariation>
            {
                new() { Id = controlId, Key = "control", Name = "Control", Value = "false" },
                new() { Id = treatmentId, Key = "treatment", Name = "Treatment", Value = "true" }
            }
        };
        db.FeatureFlags.Add(flag);

        var state = new FlagEnvironmentState
        {
            Environment = environment,
            FeatureFlag = flag,
            IsEnabled = true,
            IsMabEnabled = isMabEnabled,
            IsExperimentActive = true,
            ExperimentStartedAt = DateTimeOffset.UtcNow.AddDays(-1),
            MabGoalEvent = "mab_conversion",
            ContextPartitionKeys = contextPartitionKeys ?? [],
            FallthroughRollout = new List<VariationWeight> 
            { 
                new() { VariationId = controlId, Weight = 5000 },
                new() { VariationId = treatmentId, Weight = 5000 }
            }
        };
        db.FlagEnvironmentStates.Add(state);

        await db.SaveChangesAsync();
        return (project.Id, environment.Id, flagKey, controlId, treatmentId);
    }

    private async Task RunSimulationAsync(Guid projectId, Guid environmentId, string flagKey, Guid controlId, Guid treatmentId)
    {
        var req = new SimulateExperimentRequest
        {
            EventName = "mab_conversion",
            ParticipantsCount = 500,
            Variations = new List<SimulationVariantDto>
            {
                new() { VariationId = controlId, ConversionRate = 0.05 },
                new() { VariationId = treatmentId, ConversionRate = 0.25 }
            }
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/environments/{environmentId}/flags/{flagKey}/experiments/simulate", req);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Simulation failed with {response.StatusCode}. Body: {body}");
        }
    }

    private async Task RunRollupWorkerAsync(IServiceProvider serviceProvider)
    {
        var hubContext = serviceProvider.GetRequiredService<IToggleEventPublisher>();

        using (var scope1 = serviceProvider.CreateScope())
        {
            var db = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
            var math = scope1.ServiceProvider.GetRequiredService<BayesianMathService>();
            var mabShifter = scope1.ServiceProvider.GetRequiredService<IMabTrafficShifterService>();
            var notifyHandler = new NotifyFlagUpdatedCommandHandler(
                scope1.ServiceProvider.GetRequiredService<IConnectionMultiplexer>(),
                scope1.ServiceProvider.GetRequiredService<ICacheInvalidator>(),
                hubContext,
                scope1.ServiceProvider.GetRequiredService<ILogger<NotifyFlagUpdatedCommandHandler>>(),
                scope1.ServiceProvider.GetRequiredService<IConfiguration>()
            );
            await mabShifter.ProcessMabTrafficShiftingAsync(db, math, notifyHandler, CancellationToken.None);
        }

        using (var scope2 = serviceProvider.CreateScope())
        {
            var db = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
            var math = scope2.ServiceProvider.GetRequiredService<BayesianMathService>();
            var mabShifter = scope2.ServiceProvider.GetRequiredService<IMabTrafficShifterService>();
            var notifyHandler = new NotifyFlagUpdatedCommandHandler(
                scope2.ServiceProvider.GetRequiredService<IConnectionMultiplexer>(),
                scope2.ServiceProvider.GetRequiredService<ICacheInvalidator>(),
                hubContext,
                scope2.ServiceProvider.GetRequiredService<ILogger<NotifyFlagUpdatedCommandHandler>>(),
                scope2.ServiceProvider.GetRequiredService<IConfiguration>()
            );
            await mabShifter.ProcessContextualBanditAutoSegmentationAsync(db, math, notifyHandler, CancellationToken.None);
        }
    }

    [Fact]
    public async Task CaseA_MabOff_ShouldAggregateButNotUpdateRollouts()
    {
        // Arrange
        var (projectId, environmentId, flagKey, controlId, treatmentId) = await SeedDataAsync("flag_srm_test", isMabEnabled: false, null);

        // Act
        await RunSimulationAsync(projectId, environmentId, flagKey, controlId, treatmentId);

        using var scope = _factory.Services.CreateScope();
        var queryEngine = scope.ServiceProvider.GetRequiredService<IAnalyticsQueryEngine>();
        await queryEngine.AggregateMetricsAsync(CancellationToken.None);
        await queryEngine.AggregateContextualMetricsAsync(CancellationToken.None);
        await RunRollupWorkerAsync(scope.ServiceProvider);

        // Assert
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.FlagEnvironmentStates.AsNoTracking().SingleAsync(s => s.EnvironmentId == environmentId && s.FeatureFlag.Key == flagKey);

        state.FallthroughRollout!.First().Weight.Should().Be(5000, "MAB is off, base rollout should not change");
        state.ContextualRollouts.Should().BeNullOrEmpty("No contextual MAB was enabled");
    }

    [Fact]
    public async Task CaseB_GlobalMab_ShouldUpdateBaseRollout()
    {
        // Arrange
        var (projectId, environmentId, flagKey, controlId, treatmentId) = await SeedDataAsync("global-mab-flag", true, null);
        await RunSimulationAsync(projectId, environmentId, flagKey, controlId, treatmentId);

        using var scope = _factory.Services.CreateScope();
        var queryEngine = scope.ServiceProvider.GetRequiredService<IAnalyticsQueryEngine>();
        await queryEngine.AggregateMetricsAsync(CancellationToken.None);
        await RunRollupWorkerAsync(scope.ServiceProvider);

        // Assert
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.FlagEnvironmentStates.AsNoTracking().SingleAsync(s => s.EnvironmentId == environmentId && s.FeatureFlag.Key == flagKey);

        state.FallthroughRollout!.Last().Weight.Should().BeGreaterThan(5000, "Treatment won significantly, so MAB should shift traffic towards 100%");
        state.ContextualRollouts.Should().BeNullOrEmpty("No contextual partition keys were provided");
    }

    [Fact]
    public async Task CaseC_ContextualMab_ShouldUpdateContextualRollouts()
    {
        // Arrange
        var (projectId, environmentId, flagKey, controlId, treatmentId) = await SeedDataAsync("contextual-mab-flag", true, ["country"]);

        // Act
        await RunSimulationAsync(projectId, environmentId, flagKey, controlId, treatmentId);

        using var scope = _factory.Services.CreateScope();
        var queryEngine = scope.ServiceProvider.GetRequiredService<IAnalyticsQueryEngine>();
        await queryEngine.AggregateMetricsAsync(CancellationToken.None);
        await queryEngine.AggregateContextualMetricsAsync(CancellationToken.None);
        await RunRollupWorkerAsync(scope.ServiceProvider);

        // Assert
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.FlagEnvironmentStates.AsNoTracking().Include(x => x.ContextualRollouts).SingleAsync(s => s.EnvironmentId == environmentId && s.FeatureFlag.Key == flagKey);

        state.FallthroughRollout!.Last().Weight.Should().BeGreaterThan(5000, "Base rollout should also shift based on overall global traffic");
        state.ContextualRollouts.Should().NotBeNullOrEmpty();

        var keys = state.ContextualRollouts!.Select(x => x.ContextSlice).ToList();
        keys.Should().Contain(k => k.Contains("US") || k.Contains("CA") || k.Contains("GB") || k.Contains("AU"));

        foreach (var value in state.ContextualRollouts.Select(x => x.Rollout!.Last().Weight))
            value.Should().BeGreaterThan(5000, "Treatment CR is uniformly higher in our simulator, so all countries should shift to treatment");
    }
}
