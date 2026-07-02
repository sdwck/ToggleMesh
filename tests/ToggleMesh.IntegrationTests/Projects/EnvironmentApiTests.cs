using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Projects.CreateEnvironment;
using ToggleMesh.API.Features.Projects.CreateKey;
using ToggleMesh.API.Features.Projects.CreateProject;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Projects.GetKeys;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Projects;

[Collection("SharedEnv2")]
public class EnvironmentApiTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
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
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{project!.Id}/environments", request);

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
    public async Task CreateEnvironmentKey_ShouldReturnKeyAndSave()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest { Name = "Key Test Project" });
        var project = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>();

        var envResponse = await _client.PostAsJsonAsync($"/api/v1/projects/{project!.Id}/environments", new CreateEnvironmentRequest { Name = "Prod" });
        var env = await envResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();

        var request = new CreateKeyRequest { Name = "Production Server Key", Type = KeyType.Server };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env!.Id}/keys", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CreateKeyResponse>();

        result.Should().NotBeNull();
        result.Name.Should().Be("Production Server Key");
        result.KeyType.Should().Be(KeyType.Server);
        result.PlainKey.Should().StartWith("tm_server_");
        result.KeyPreview.Should().NotBeNullOrEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbKey = await db.EnvironmentKeys.FirstOrDefaultAsync(k => k.Id == result.Id);
        dbKey.Should().NotBeNull();
        dbKey.Name.Should().Be("Production Server Key");
        dbKey.KeyType.Should().Be(KeyType.Server);
    }

    [Fact]
    public async Task GetEnvironmentKeys_ShouldReturnAllKeys()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest { Name = "Key Get Project" });
        var project = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>();

        var envResponse = await _client.PostAsJsonAsync($"/api/v1/projects/{project!.Id}/environments", new CreateEnvironmentRequest { Name = "Prod" });
        var env = await envResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();

        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env!.Id}/keys", new CreateKeyRequest { Name = "Server Key", Type = KeyType.Server });
        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/keys", new CreateKeyRequest { Name = "Client Key", Type = KeyType.Client });

        // Act
        var response = await _client.GetAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/keys");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<GetKeysResponse>>();

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().ContainSingle(k => k.Name == "Server Key" && k.KeyType == KeyType.Server);
        result.Should().ContainSingle(k => k.Name == "Client Key" && k.KeyType == KeyType.Client);
    }

    [Fact]
    public async Task RevokeEnvironmentKey_ShouldRemoveKey()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest { Name = "Key Revoke Project" });
        var project = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>();

        var envResponse = await _client.PostAsJsonAsync($"/api/v1/projects/{project!.Id}/environments", new CreateEnvironmentRequest { Name = "Prod" });
        var env = await envResponse.Content.ReadFromJsonAsync<CreateEnvironmentResponse>();

        var keyResponse = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env!.Id}/keys", new CreateKeyRequest { Name = "Key To Revoke", Type = KeyType.Server });
        var key = await keyResponse.Content.ReadFromJsonAsync<CreateKeyResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/keys/{key!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbKey = await db.EnvironmentKeys.FirstOrDefaultAsync(k => k.Id == key.Id);
        dbKey.Should().BeNull();
    }
}
