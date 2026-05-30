using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using ToggleMesh.API.Features.Flags;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Toggle;
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

    [Fact]
    public async Task CreateFlag_WithValidData_ShouldReturn201Created()
    {
        // Arrange
        var request = new CreateFlagRequest { Key = "test_feature_1" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/flags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<FeatureFlag>();
        result.Should().NotBeNull();
        result!.Key.Should().Be("test_feature_1");
        result.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CreateFlag_WithInvalidData_ShouldReturn400BadRequest()
    {
        // Arrange
        var request = new CreateFlagRequest { Key = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/flags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ToggleFlag_ShouldUpdateDb_AndBroadcastViaSignalR()
    {
        var flagKey = "signalr_test_flag";
        await _client.PostAsJsonAsync("/api/flags", new CreateFlagRequest { Key = flagKey });
        
        var tcs = new TaskCompletionSource<(string Key, bool IsEnabled)>();
        
        var hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/toggle", options => 
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        hubConnection.On<string, bool>("FlagUpdated", (key, isEnabled) =>
        {
            tcs.SetResult((key, isEnabled));
        });

        await hubConnection.StartAsync();
        
        var toggleRequest = new ToggleFlagRequest { Key = flagKey, IsEnabled = true };
        var response = await _client.PostAsJsonAsync("/api/flags/toggle", toggleRequest);
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var signalRTask = tcs.Task;
        var completedTask = await Task.WhenAny(signalRTask, Task.Delay(2000));
        
        completedTask.Should().Be(signalRTask, "SignalR event was not received within 2 seconds");
        
        var (receivedKey, receivedStatus) = await signalRTask;
        receivedKey.Should().Be(flagKey);
        receivedStatus.Should().BeTrue();
    }
}