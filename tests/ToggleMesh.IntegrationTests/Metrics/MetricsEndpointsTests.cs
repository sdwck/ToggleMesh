using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Metrics.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Metrics;

[Collection("SharedEnv2")]
public class MetricsEndpointsTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public MetricsEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid EnvironmentId, string ApiKey)> SeedEnvironmentAndFlagAsync(string flagKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Metrics Test Project" };
        db.Projects.Add(project); db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

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
        return (environment.Id, plainKey);
    }

    [Fact]
    public async Task IngestMetrics_ShouldReturn202_AndWorkerShouldUpdateDb()
    {
        const string flagKey = "test_metrics_flag";
        var (_, apiKey) = await SeedEnvironmentAndFlagAsync(flagKey);

        var trueVarId = Guid.NewGuid();
        var falseVarId = Guid.NewGuid();
        var payload = new List<MetricPayloadDto>
        {
            new(flagKey, new List<MetricVariationPayloadDto> { new(trueVarId, 15), new(falseVarId, 5) }),
            new(flagKey, new List<MetricVariationPayloadDto> { new(trueVarId, 10), new(falseVarId, 2) })
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sdk/metrics")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Add("x-api-key", apiKey);

        var response = await _client.SendAsync(httpRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(6));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var i = 0; i < 10; i++)
        {
            var totalTrue = await db.FlagMetricBuckets.Where(b => b.FlagKey == flagKey && b.VariationId == trueVarId).SumAsync(b => (long?)b.Count) ?? 0;
            if (totalTrue == 25)
                break;
            await Task.Delay(200);
        }

        var trueCount = db.FlagMetricBuckets.Where(b => b.FlagKey == flagKey && b.VariationId == trueVarId).Sum(b => (long?)b.Count) ?? 0;
        var falseCount = db.FlagMetricBuckets.Where(b => b.FlagKey == flagKey && b.VariationId == falseVarId).Sum(b => (long?)b.Count) ?? 0;
        trueCount.Should().Be(25);
        falseCount.Should().Be(7);
    }

    [Fact]
    public async Task IngestMetrics_WithInvalidApiKey_ShouldReturn401()
    {
        var payload = new List<MetricPayloadDto> { new("some_flag", new List<MetricVariationPayloadDto> { new(Guid.NewGuid(), 1) }) };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sdk/metrics")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Add("x-api-key", "invalid_key");

        var response = await _client.SendAsync(httpRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
