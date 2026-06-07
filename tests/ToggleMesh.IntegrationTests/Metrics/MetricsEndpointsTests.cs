using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags;
using ToggleMesh.API.Features.Metrics.Ingest;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Persistence;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Metrics;

public class MetricsEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
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
        db.Projects.Add(project); db.ProjectMembers.Add(new ToggleMesh.API.Features.Projects.ProjectMember { Project = project, UserId = Guid.Parse(ToggleMesh.IntegrationTests.Infrastructure.TestAuthHandler.TestUserId), Role = ToggleMesh.API.Features.Projects.ProjectRole.Owner });

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
            IsEnabled = true,
            TrueCount = 0,
            FalseCount = 0
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
        
        var payload = new List<MetricPayloadDto>
        {
            new(flagKey, 15, 5),
            new(flagKey, 10, 2)
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
            var stateToCheck = await db.FlagEnvironmentStates
                .Include(s => s.FeatureFlag)
                .AsNoTracking()
                .SingleAsync(f => f.FeatureFlag.Key == flagKey);
            if (stateToCheck.TrueCount == 25) 
                break;
            await Task.Delay(200);
        }

        var state = db.FlagEnvironmentStates.Include(s => s.FeatureFlag).Single(f => f.FeatureFlag.Key == flagKey);
        state.TrueCount.Should().Be(25);
        state.FalseCount.Should().Be(7);
    }
    
    [Fact]
    public async Task IngestMetrics_WithInvalidApiKey_ShouldReturn401()
    {
        var payload = new List<MetricPayloadDto> { new("some_flag", 1, 0) };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sdk/metrics")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Add("x-api-key", "invalid_key");

        var response = await _client.SendAsync(httpRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
