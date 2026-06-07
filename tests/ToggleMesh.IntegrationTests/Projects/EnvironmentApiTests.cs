using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Projects.CreateEnvironment;
using ToggleMesh.API.Features.Projects.CreateProject;
using ToggleMesh.API.Features.Projects.RotateEnvironmentKey;
using ToggleMesh.API.Persistence;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Projects;

public class EnvironmentApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public EnvironmentApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateEnvironment_ShouldReturn200_AndAddToProject()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest { Name = "Env Test Project" });
        var project = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>();
        var request = new CreateEnvironmentRequest { Name = "Staging" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();
        result.Should().NotBeNull();
        result.Name.Should().Be("Staging");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbEnv = await db.Environments.FirstOrDefaultAsync(e => e.Id == result.Id);
        dbEnv.Should().NotBeNull();
        dbEnv.ProjectId.Should().Be(project.Id);
    }

    [Fact]
    public async Task RotateEnvironmentKey_ShouldReturnNewKey_AndRevokeOldKey()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest { Name = "Key Test Project" });
        var project = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>();
        
        var envResponse = await _client.PostAsJsonAsync($"/api/v1/projects/{project!.Id}/environments", new CreateEnvironmentRequest { Name = "Prod" });
        var env = await envResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();
        
        var keyResponse1 = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/keys/rotate", new {});
        var key1 = await keyResponse1.Content.ReadFromJsonAsync<RotateEnvironmentKeyResponse>();

        // Act
        var keyResponse2 = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/keys/rotate", new {});

        // Assert
        keyResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var key2 = await keyResponse2.Content.ReadFromJsonAsync<RotateEnvironmentKeyResponse>();
        
        key2.Should().NotBeNull();
        key2.ApiKey.Should().NotBeNullOrEmpty();
        key2.ApiKey.Should().NotBe(key1!.ApiKey);
        key2.ApiKey.Should().StartWith("tm_");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbKeys = await db.EnvironmentKeys.Where(k => k.EnvironmentId == env.Id).ToListAsync();
        
        dbKeys.Should().HaveCount(2);
        dbKeys.Should().ContainSingle(k => k.ExpireOn == null);
        dbKeys.Should().ContainSingle(k => k.ExpireOn != null);
    }
}
