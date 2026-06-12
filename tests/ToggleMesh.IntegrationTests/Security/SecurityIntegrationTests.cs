using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Projects.EnvironmentKeys;
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
    public async Task CreateAndRevokeKey_ShouldWorkCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Key CRUD Test" };
        db.Projects.Add(project); db.ProjectMembers.Add(new ToggleMesh.API.Features.Projects.ProjectMember { Project = project, UserId = Guid.Parse(ToggleMesh.IntegrationTests.Infrastructure.TestAuthHandler.TestUserId), Role = ToggleMesh.API.Features.Projects.ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Prod", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        // Act
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/environments/{env.Id}/keys", 
            new CreateKeyRequest 
            { 
                Name = "Server Key", 
                Type = KeyType.Server 
            });
        
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateKeyResponse>();
        createResult.Should().NotBeNull();
        createResult.PlainKey.Should().StartWith("tm_server_");

        // Assert
        var sdkRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sdk/flags");
        sdkRequest.Headers.Add("x-api-key", createResult.PlainKey);
        var accessResponse = await _client.SendAsync(sdkRequest);
        accessResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var revokeResponse = await _client.DeleteAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/keys/{createResult.Id}");
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var sdkRequestExpired = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sdk/flags");
        sdkRequestExpired.Headers.Add("x-api-key", createResult.PlainKey);
        var expiredResponse = await _client.SendAsync(sdkRequestExpired);
        expiredResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
