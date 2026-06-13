using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ToggleMesh.API.Features.Client.SdkEvaluateFlag;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Toggle;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Persistence;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Caching;

public class CacheInvalidationTests : IClassFixture<TestWebApplicationFactory>
{
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

        var cache = _factory.Services.GetRequiredService<HybridCache>();

        var l1CacheKey = $"sdk:compiled_rules:{envId}";
        var l2CacheKey = $"sdk:flags:states:{envId}";
        
        var dummyL2Data = new List<FlagStateDto> { new(flagKey, false, null, false, []) };
        
        await cache.SetAsync(l2CacheKey, dummyL2Data);
        await cache.SetAsync(l1CacheKey, "dummy_l1_data", new HybridCacheEntryOptions 
        { 
            Flags = HybridCacheEntryFlags.DisableDistributedCache 
        });
        
        (await cache.GetOrCreateAsync<List<FlagStateDto>>(l2CacheKey, _ => ValueTask.FromResult<List<FlagStateDto>>(null!))).Should().NotBeNull();

        // Act
        var toggleRequest = new ToggleFlagRequest { IsEnabled = true };   
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/toggle", toggleRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var cachedL2 = await cache.GetOrCreateAsync<List<FlagStateDto>>(l2CacheKey, _ => ValueTask.FromResult<List<FlagStateDto>>(null!), 
            options: new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableLocalCache });
        
        cachedL2.Should().BeNull("L2 Cache (Redis) should have been cleared.");

        var cachedL1 = await cache.GetOrCreateAsync<string>(l1CacheKey, _ => ValueTask.FromResult<string>(null!));
        cachedL1.Should().BeNull("L1 Memory Cache should have been cleared by the background worker.");
    }
}