using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Projects.RotateEnvironmentKey;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Persistence;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Security;

public class SecurityIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public SecurityIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SdkAccess_WithInvalidKey_ShouldReturn401()
    {
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sdk/flags");
        request.Headers.Add("x-api-key", "invalid_key");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RotateKey_ShouldInvalidateOldKey_AndEnableNewKey()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Rotation Test" };
        db.Projects.Add(project); db.ProjectMembers.Add(new ToggleMesh.API.Features.Projects.ProjectMember { Project = project, UserId = Guid.Parse(ToggleMesh.IntegrationTests.Infrastructure.TestAuthHandler.TestUserId), Role = ToggleMesh.API.Features.Projects.ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Prod", Project = project };
        db.Environments.Add(env);
        
        var oldPlainKey = "tm_old_key_12345";
        var oldHash = ApiKeyHasher.Hash(oldPlainKey);
        db.EnvironmentKeys.Add(new EnvironmentKey 
        { 
            Environment = env, 
            KeyHash = oldHash, 
            KeyPreview = ApiKeyHasher.GeneratePreview(oldPlainKey) 
        });
        await db.SaveChangesAsync();

        var sdkRequestOld = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sdk/flags");
        sdkRequestOld.Headers.Add("x-api-key", oldPlainKey);
        var initialResponse = await _client.SendAsync(sdkRequestOld);
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var rotateResponse = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/keys/rotate", new { });
        rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotateResult = await rotateResponse.Content.ReadFromJsonAsync<RotateEnvironmentKeyResponse>();
        var newPlainKey = rotateResult!.ApiKey;

        // Assert
        var sdkRequestExpired = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sdk/flags");
        sdkRequestExpired.Headers.Add("x-api-key", oldPlainKey);
        var expiredResponse = await _client.SendAsync(sdkRequestExpired);
        expiredResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var sdkRequestNew = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sdk/flags");
        sdkRequestNew.Headers.Add("x-api-key", newPlainKey);
        var newResponse = await _client.SendAsync(sdkRequestNew);
        newResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFlag_WhenCached_ShouldStillReturnCorrectData()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Cache Test" };
        db.Projects.Add(project); db.ProjectMembers.Add(new ToggleMesh.API.Features.Projects.ProjectMember { Project = project, UserId = Guid.Parse(ToggleMesh.IntegrationTests.Infrastructure.TestAuthHandler.TestUserId), Role = ToggleMesh.API.Features.Projects.ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Dev", Project = project };
        db.Environments.Add(env);
        var flag = new API.Features.Flags.FeatureFlag { Key = "cached_flag", Project = project };
        db.FeatureFlags.Add(flag);
        var state = new API.Features.Flags.FlagEnvironmentState { FeatureFlag = flag, Environment = env, IsEnabled = true };
        db.FlagEnvironmentStates.Add(state);
        await db.SaveChangesAsync();

        var url = $"/api/v1/projects/{project.Id}/environments/{env.Id}/flags/cached_flag";
        var resp1 = await _client.GetAsync(url);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK);

        var resp2 = await _client.GetAsync(url);
        
        // Assert
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp2.Content.ReadFromJsonAsync<GetFlagResponse>();
        result!.Key.Should().Be("cached_flag");
        result.IsEnabled.Should().BeTrue();
    }
}
