using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Metrics.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Projects.GetDashboard;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Analytics;

[Collection("SharedEnv4")]
public class DashboardAndMetricsE2ETests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public DashboardAndMetricsE2ETests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid EnvironmentId, string FlagKey, string ApiKey)> SeedDataAsync(string flagKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var testOrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var project = new Project { Name = "Dashboard E2E Project", OrganizationId = testOrgId };

        db.Projects.Add(project);

        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Role = ProjectRole.Owner
        });

        var environment = new ProjectEnvironment { Name = "Production", Project = project };
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

        var flag = new FeatureFlag
        {
            Project = project,
            Key = flagKey
        };
        db.FeatureFlags.Add(flag);

        var state = new FlagEnvironmentState
        {
            Environment = environment,
            FeatureFlag = flag,
            IsEnabled = true
        };
        db.FlagEnvironmentStates.Add(state);

        await db.SaveChangesAsync();
        return (project.Id, environment.Id, flagKey, plainKey);
    }

    [Fact]
    public async Task Dashboard_ShouldCorrectlyAggregateMetrics_FromMetricsWorker()
    {
        var (projectId, environmentId, flagKey, apiKey) = await SeedDataAsync("dashboard_metrics_flag");
        
        var trueVarId = Guid.NewGuid();
        var falseVarId = Guid.NewGuid();
        var payload = new List<MetricPayloadDto>
        {
            new(flagKey, [new MetricVariationPayloadDto(trueVarId, 10), new MetricVariationPayloadDto(falseVarId, 5)])
        };

        for (var i = 0; i < 3; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sdk/metrics")
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("x-api-key", apiKey);

            var res = await _client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(10));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var i = 0; i < 20; i++)
        {
            _factory.TimeProvider.Advance(TimeSpan.FromSeconds(5));
            var totalTrue = await db.FlagMetricBuckets.Where(b => b.FlagKey == flagKey && b.VariationId == trueVarId).SumAsync(b => (long?)b.Count) ?? 0;
            if (totalTrue == 30)
                break;
            await Task.Delay(200);
        }

        var trueCount = await db.FlagMetricBuckets.Where(b => b.FlagKey == flagKey && b.VariationId == trueVarId).SumAsync(b => (long?)b.Count) ?? 0;
        var falseCount = await db.FlagMetricBuckets.Where(b => b.FlagKey == flagKey && b.VariationId == falseVarId).SumAsync(b => (long?)b.Count) ?? 0;

        trueCount.Should().Be(30, "We sent 3 batches of 10 true evaluations");
        falseCount.Should().Be(15, "We sent 3 batches of 5 false evaluations");

        var dashboardRes = await _client.GetAsync($"/api/v1/projects/{projectId}/dashboard?environmentId={environmentId}");
        dashboardRes.EnsureSuccessStatusCode();

        var dashboardData = await dashboardRes.Content.ReadFromJsonAsync<ProjectDashboardDto>();

        dashboardData.Should().NotBeNull();
        dashboardData.ActiveFlagsCount.Should().Be(1);

        var totalEvaluationsFromGraph = dashboardData.EvaluationsLast24Hours.Sum(p => p.Count);
        totalEvaluationsFromGraph.Should().Be(45, "30 true + 15 false = 45 total evaluations in the graph");
    }
}
