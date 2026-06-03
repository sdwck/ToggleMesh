using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags;
using ToggleMesh.API.Features.Metrics.Ingest;
using ToggleMesh.API.Features.Projects;
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
        db.Projects.Add(project);

        var environment = new ProjectEnvironment { Name = "Production", Project = project };
        db.Environments.Add(environment);

        var key = new EnvironmentKey
        {
            Environment = environment,
            ApiKey = Guid.NewGuid().ToString("N"),
            CreatedOn = DateTime.UtcNow
        };
        db.EnvironmentKeys.Add(key);

        var flag = new FeatureFlag
        {
            Environment = environment,
            Key = flagKey,
            IsEnabled = true,
            TrueCount = 0,
            FalseCount = 0
        };
        db.FeatureFlags.Add(flag);

        await db.SaveChangesAsync();
        return (environment.Id, key.ApiKey);
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

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sdk/metrics")
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
            var flagToCheck = await db.FeatureFlags.AsNoTracking().SingleAsync(f => f.Key == flagKey);
            if (flagToCheck.TrueCount == 25) 
                break;
            await Task.Delay(200);
        }

        var flag = db.FeatureFlags.Single(f => f.Key == flagKey);
        flag.TrueCount.Should().Be(25);
        flag.FalseCount.Should().Be(7);
    }
    
    [Fact]
    public async Task IngestMetrics_WithInvalidApiKey_ShouldReturn401()
    {
        var payload = new List<MetricPayloadDto> { new("some_flag", 1, 0) };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/sdk/metrics")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Add("x-api-key", "invalid_key");

        var response = await _client.SendAsync(httpRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}