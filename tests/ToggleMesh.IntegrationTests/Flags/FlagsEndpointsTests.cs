using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Flags.Toggle;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Persistence;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Flags;

public class FlagsEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public FlagsEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid EnvironmentId, string ApiKey)> SeedEnvironmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Test Project" };
        db.Projects.Add(project); db.ProjectMembers.Add(new ToggleMesh.API.Features.Projects.ProjectMember { Project = project, UserId = Guid.Parse(ToggleMesh.IntegrationTests.Infrastructure.TestAuthHandler.TestUserId), Role = ToggleMesh.API.Features.Projects.ProjectRole.Owner });

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
        return (project.Id, environment.Id, plainKey);
    }

    [Fact]
    public async Task CreateFlag_WithValidData_ShouldReturn201Created()
    {
        // Arrange
        var (projectId, _, _) = await SeedEnvironmentAsync();
        var request = new CreateFlagRequest { Key = "test_feature_1" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<GetFlagResponse>();
        result.Should().NotBeNull();
        result.Key.Should().Be("test_feature_1");
        result.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CreateFlag_WithInvalidData_ShouldReturn400BadRequest()
    {
        // Arrange
        var (projectId, _, _) = await SeedEnvironmentAsync();
        var request = new CreateFlagRequest { Key = "" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ToggleFlag_ShouldUpdateDb_AndBroadcastViaSignalR()
    {
        // Arrange
        var (projectId, envId, apiKey) = await SeedEnvironmentAsync();
        var flagKey = "signalr_test_flag";
        await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags", new CreateFlagRequest { Key = flagKey });

        var tcs = new TaskCompletionSource<GetFlagResponse>();

        var hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost/api/v1/hubs/toggle", options =>
            {
                options.Headers.Add("x-api-key", apiKey);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        hubConnection.On<GetFlagResponse>("FlagUpdated", flag =>
        {
            tcs.SetResult(flag);
        });

        await hubConnection.StartAsync();
        
        await Task.Delay(500);

        // Act
        var toggleRequest = new ToggleFlagRequest { IsEnabled = true };   
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/toggle", toggleRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var signalRTask = tcs.Task;
        var completedTask = await Task.WhenAny(signalRTask, Task.Delay(15000));

        completedTask.Should().Be(signalRTask, "SignalR event was not received within 15 seconds");

        var receivedFlag = await signalRTask;
        receivedFlag.Key.Should().Be(flagKey);
        receivedFlag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteFlag_WithValidData_ShouldReturn204NoContent()
    {
        // Arrange
        var (projectId, _, _) = await SeedEnvironmentAsync();
        var flagKey = "to_delete_flag";
        await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags", new CreateFlagRequest { Key = flagKey });

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/v1/projects/{projectId}/flags/{flagKey}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.FeatureFlags.Any(f => f.ProjectId == projectId && f.Key == flagKey).Should().BeFalse();
    }
}
