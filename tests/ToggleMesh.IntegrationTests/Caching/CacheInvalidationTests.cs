using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ToggleMesh.API.Features.Client.SdkEvaluateFlag;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Toggle;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Caching;

[Collection("SharedEnv3")]
public class CacheInvalidationTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public CacheInvalidationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid EnvironmentId)> SeedEnvironmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Test Project Caching" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Role = ProjectRole.Owner
        });

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
        return (project.Id, environment.Id);
    }

    [Fact]
    public async Task TogglingFlag_ShouldInvalidate_L1AndL2Caches()
    {
        // Arrange
        var (projectId, envId) = await SeedEnvironmentAsync();
        var flagKey = "cache_test_flag";

        var createResponse = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags", new CreateFlagRequest { Key = flagKey });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var redis = _factory.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var memoryCache = _factory.Services.GetRequiredService<IMemoryCache>();

        var l1CacheKey = $"sdk:compiled_rules:{envId}";
        var l2CacheKey = $"sdk:flags:states:{envId}";

        var dummyL2Data = new List<FlagStateDto> { new(flagKey, false, null, [], false, [], null, null, null, null) };

        await redis.StringSetAsync(l2CacheKey, JsonSerializer.Serialize(dummyL2Data));
        memoryCache.Set(l1CacheKey, "dummy_l1_data");

        (await redis.StringGetAsync(l2CacheKey)).HasValue.Should().BeTrue();

        // Act
        var toggleRequest = new ToggleFlagRequest { IsEnabled = true };
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/toggle", toggleRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cachedL2 = await redis.StringGetAsync(l2CacheKey);
        cachedL2.HasValue.Should().BeFalse("L2 Cache (Redis) should have been cleared.");

        var cachedL1 = memoryCache.TryGetValue(l1CacheKey, out _);
        cachedL1.Should().BeFalse("L1 Memory Cache should have been cleared by the background worker.");
    }
}
