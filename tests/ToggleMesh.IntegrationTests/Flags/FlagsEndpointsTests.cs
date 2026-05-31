using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Toggle;
using ToggleMesh.API.Features.Projects;
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

    private async Task<(Guid EnvironmentId, string ApiKey)> SeedEnvironmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Test Project" };
        db.Projects.Add(project);

        var environment = new ProjectEnvironment { Name = "Development", Project = project };
        db.Environments.Add(environment);

        var key = new EnvironmentKey
        {
            Environment = environment,
            ApiKey = Guid.NewGuid().ToString("N"),
            CreatedOn = DateTime.UtcNow
        };
        db.EnvironmentKeys.Add(key);

        await db.SaveChangesAsync();
        return (environment.Id, key.ApiKey);
    }

    [Fact]
    public async Task CreateFlag_WithValidData_ShouldReturn201Created()
    {
        // Arrange
        var (envId, _) = await SeedEnvironmentAsync();
        var request = new CreateFlagRequest { EnvironmentId = envId, Key = "test_feature_1" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/flags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<FeatureFlag>();
        result.Should().NotBeNull();
        result.Key.Should().Be("test_feature_1");
        result.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CreateFlag_WithInvalidData_ShouldReturn400BadRequest()
    {
        // Arrange
        var (envId, _) = await SeedEnvironmentAsync();
        var request = new CreateFlagRequest { EnvironmentId = envId, Key = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/flags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ToggleFlag_ShouldUpdateDb_AndBroadcastViaSignalR()
    {
        // Arrange
        var (envId, apiKey) = await SeedEnvironmentAsync();
        var flagKey = "signalr_test_flag";
        await _client.PostAsJsonAsync("/api/flags", new CreateFlagRequest { EnvironmentId = envId, Key = flagKey });
        
        var tcs = new TaskCompletionSource<(string Key, bool IsEnabled)>();
        
        var hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/toggle", options => 
            {
                options.Headers.Add("x-api-key", apiKey);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        hubConnection.On<string, bool>("FlagUpdated", (key, isEnabled) =>
        {
            tcs.SetResult((key, isEnabled));
        });

        await hubConnection.StartAsync();
        await Task.Delay(500);
        
        // Act
        var toggleRequest = new ToggleFlagRequest { EnvironmentId = envId, Key = flagKey, IsEnabled = true };
        var response = await _client.PostAsJsonAsync("/api/flags/toggle", toggleRequest);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var signalRTask = tcs.Task;
        var completedTask = await Task.WhenAny(signalRTask, Task.Delay(5000));
        
        completedTask.Should().Be(signalRTask, "SignalR event was not received within 5 seconds");
        
        var (receivedKey, receivedStatus) = await signalRTask;
        receivedKey.Should().Be(flagKey);
        receivedStatus.Should().BeTrue();
    }
}