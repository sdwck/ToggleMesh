using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Analytics.Ingest;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Experiments.Start;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Flags.Update;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.IntegrationTests.Infrastructure;
using DomainVariationWeight = ToggleMesh.API.Features.Flags.Domain.VariationWeight;

namespace ToggleMesh.IntegrationTests.Flags;

[Collection("SharedEnv3")]
public class FlagsRegressionTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FlagsRegressionTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(Guid ProjectId, Guid EnvironmentId, string ApiKey)> SeedEnvironmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Test Project" };
        db.Projects.Add(project); 
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var environment = new ProjectEnvironment { Name = "Development", Project = project };
        db.Environments.Add(environment);

        var plainKey = Guid.NewGuid().ToString("N");
        var keyHash = ApiKeyHasher.Hash(plainKey);
        var key = new EnvironmentKey
        {
            Environment = environment,
            KeyHash = keyHash,
            KeyPreview = ApiKeyHasher.GeneratePreview(keyHash),
            CreatedOn = DateTime.UtcNow
        };
        db.EnvironmentKeys.Add(key);
        await db.SaveChangesAsync();

        return (project.Id, environment.Id, plainKey);
    }

    [Fact]
    public async Task UpdateFlagEndpoint_ActiveExperiment_IgnoresRulesAndRollout()
    {
        var env = await SeedEnvironmentAsync();
        
        var createReq = new CreateFlagRequest { Key = "exp-flag", Type = FlagType.Boolean };
        var res1 = await _client.PostAsJsonAsync($"/api/v1/projects/{env.ProjectId}/flags", createReq);
        res1.EnsureSuccessStatusCode();

        var startReq = new StartExperimentRequest { Mode = "mab", GoalEvent = "click", OptimizationType = MabOptimizationType.Conversion, MabExplorationFloor = 5 };
        var startRes = await _client.PostAsJsonAsync($"/api/v1/projects/{env.ProjectId}/environments/{env.EnvironmentId}/flags/exp-flag/experiments/start", startReq);
        startRes.EnsureSuccessStatusCode();

        var updateReq = new UpdateFlagRequest
        {
            FallthroughRollout = [new DomainVariationWeight { VariationId = Guid.NewGuid(), Weight = 10000 }],
            Rules = [new RuleInput(1, "email", "eq", "test@test.com", [])]
        };

        var updateRes = await _client.PutAsJsonAsync($"/api/v1/projects/{env.ProjectId}/environments/{env.EnvironmentId}/flags/exp-flag", updateReq);
        updateRes.EnsureSuccessStatusCode();

        var getRes = await _client.GetFromJsonAsync<GetFlagResponse>($"/api/v1/projects/{env.ProjectId}/environments/{env.EnvironmentId}/flags/exp-flag");
        
        getRes!.FallthroughRollout.Should().NotBeEquivalentTo(updateReq.FallthroughRollout);
        getRes.Rules.Should().BeEmpty();
    }

    [Fact]
    public async Task StartExperimentEndpoint_ClearsContextualRollouts()
    {
        var env = await SeedEnvironmentAsync();
        
        var createReq = new CreateFlagRequest { Key = "ctx-flag", Type = FlagType.Boolean };
        var res = await _client.PostAsJsonAsync($"/api/v1/projects/{env.ProjectId}/flags", createReq);
        res.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var state = await db.FlagEnvironmentStates.FirstAsync(x => x.FeatureFlag.Key == "ctx-flag");
            db.ContextualRollouts.Add(new ContextualRollout { FlagEnvironmentStateId = state.Id, ContextSlice = "test", Rollout = new List<DomainVariationWeight>() });
            await db.SaveChangesAsync();
        }

        var startReq = new StartExperimentRequest { Mode = "mab", GoalEvent = "click", OptimizationType = MabOptimizationType.Conversion, MabExplorationFloor = 5 };
        var startRes = await _client.PostAsJsonAsync($"/api/v1/projects/{env.ProjectId}/environments/{env.EnvironmentId}/flags/ctx-flag/experiments/start", startReq);
        startRes.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var count = await db.ContextualRollouts.CountAsync();
            count.Should().Be(0);
        }
    }

    [Fact]
    public async Task MabTrafficShifter_BelowThreshold_DoesNotUpdate()
    {
        var env = await SeedEnvironmentAsync();
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var flag = new FeatureFlag { Key = "thresh-flag", Name = "T", ProjectId = env.ProjectId, Type = FlagType.Boolean };
        flag.Variations.Add(new FlagVariation { Key = "A", Value = "A" });
        flag.Variations.Add(new FlagVariation { Key = "B", Value = "B" });
        db.FeatureFlags.Add(flag);
        await db.SaveChangesAsync();

        var state = new FlagEnvironmentState
        {
            EnvironmentId = env.EnvironmentId,
            FeatureFlagId = flag.Id,
            IsEnabled = true,
            IsExperimentActive = true,
            IsMabEnabled = true,
            MabGoalEvent = "click",
            MabExplorationFloor = 5,
            FallthroughRollout = new List<DomainVariationWeight>
            {
                new DomainVariationWeight { VariationId = flag.Variations.ElementAt(0).Id, Weight = 5000 },
                new DomainVariationWeight { VariationId = flag.Variations.ElementAt(1).Id, Weight = 5000 }
            }
        };
        db.FlagEnvironmentStates.Add(state);
        
        db.ExperimentMetrics.Add(new ExperimentMetric { EnvironmentId = env.EnvironmentId, FlagKey = "thresh-flag", EventName = "click", VariationId = flag.Variations.ElementAt(0).Id, TotalExposures = 1000, TotalConversions = 100, LastCalculatedAt = DateTimeOffset.UtcNow });
        db.ExperimentMetrics.Add(new ExperimentMetric { EnvironmentId = env.EnvironmentId, FlagKey = "thresh-flag", EventName = "click", VariationId = flag.Variations.ElementAt(1).Id, TotalExposures = 1000, TotalConversions = 100, LastCalculatedAt = DateTimeOffset.UtcNow });
        
        await db.SaveChangesAsync();

        var shifter = new MabTrafficShifterService(scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MabTrafficShifterService>>());
        var math = new BayesianMathService();
        var realNotify = new ToggleMesh.API.Features.Flags.Commands.NotifyFlagUpdatedCommandHandler(
            scope.ServiceProvider.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(),
            scope.ServiceProvider.GetRequiredService<ToggleMesh.API.Infrastructure.Caching.ICacheInvalidator>(),
            scope.ServiceProvider.GetRequiredService<ToggleMesh.API.Infrastructure.Streaming.IToggleEventPublisher>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ToggleMesh.API.Features.Flags.Commands.NotifyFlagUpdatedCommandHandler>>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>());

        await shifter.ProcessMabTrafficShiftingAsync(db, math, realNotify, CancellationToken.None);

        var updatedState = await db.FlagEnvironmentStates.AsNoTracking().FirstAsync(x => x.Id == state.Id);
        updatedState.FallthroughRollout.ElementAt(0).Weight.Should().BeInRange(4900, 5100);
    }

    [Fact]
    public async Task MabTrafficShifter_AboveThreshold_CreatesAuditLog()
    {
        var env = await SeedEnvironmentAsync();
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var flag = new FeatureFlag { Key = "audit-flag", Name = "A", ProjectId = env.ProjectId, Type = FlagType.Boolean };
        flag.Variations.Add(new FlagVariation { Key = "A", Value = "A" });
        flag.Variations.Add(new FlagVariation { Key = "B", Value = "B" });
        db.FeatureFlags.Add(flag);
        await db.SaveChangesAsync();

        var state = new FlagEnvironmentState
        {
            EnvironmentId = env.EnvironmentId,
            FeatureFlagId = flag.Id,
            IsEnabled = true,
            IsExperimentActive = true,
            IsMabEnabled = true,
            MabGoalEvent = "click",
            MabExplorationFloor = 5,
            FallthroughRollout = new List<DomainVariationWeight>
            {
                new DomainVariationWeight { VariationId = flag.Variations.ElementAt(0).Id, Weight = 5000 },
                new DomainVariationWeight { VariationId = flag.Variations.ElementAt(1).Id, Weight = 5000 }
            }
        };
        db.FlagEnvironmentStates.Add(state);
        
        db.ExperimentMetrics.Add(new ExperimentMetric { EnvironmentId = env.EnvironmentId, FlagKey = "audit-flag", EventName = "click", VariationId = flag.Variations.ElementAt(0).Id, TotalExposures = 1000, TotalConversions = 1000, LastCalculatedAt = DateTimeOffset.UtcNow });
        db.ExperimentMetrics.Add(new ExperimentMetric { EnvironmentId = env.EnvironmentId, FlagKey = "audit-flag", EventName = "click", VariationId = flag.Variations.ElementAt(1).Id, TotalExposures = 1000, TotalConversions = 0, LastCalculatedAt = DateTimeOffset.UtcNow });
        
        await db.SaveChangesAsync();

        var shifter = new MabTrafficShifterService(scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MabTrafficShifterService>>());
        var math = new BayesianMathService();
        var realNotify = new ToggleMesh.API.Features.Flags.Commands.NotifyFlagUpdatedCommandHandler(
            scope.ServiceProvider.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>(),
            scope.ServiceProvider.GetRequiredService<ToggleMesh.API.Infrastructure.Caching.ICacheInvalidator>(),
            scope.ServiceProvider.GetRequiredService<ToggleMesh.API.Infrastructure.Streaming.IToggleEventPublisher>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ToggleMesh.API.Features.Flags.Commands.NotifyFlagUpdatedCommandHandler>>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>());

        await shifter.ProcessMabTrafficShiftingAsync(db, math, realNotify, CancellationToken.None);

        var updatedState = await db.FlagEnvironmentStates.AsNoTracking().FirstAsync(x => x.Id == state.Id);
        updatedState.FallthroughRollout.ElementAt(0).Weight.Should().BeGreaterThan(5000);

        var auditLog = await db.Set<ToggleMesh.API.Features.Audit.Domain.AuditLog>()
            .OrderByDescending(x => x.Timestamp)
            .FirstOrDefaultAsync(x => x.EntityFriendlyName == "audit-flag" && x.EntityName == "FlagEnvironmentState" && x.Action == "Modified");

        auditLog.Should().NotBeNull();
        auditLog.PerformedByEmail.Should().Be("mab-automation@togglemesh.com");
    }
}
