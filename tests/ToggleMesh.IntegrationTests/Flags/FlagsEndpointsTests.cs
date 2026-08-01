using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Flags.Toggle;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace ToggleMesh.IntegrationTests.Flags;

[Collection("SharedEnv3")]
public class FlagsEndpointsTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private readonly ITestOutputHelper _testOutputHelper;

    public FlagsEndpointsTests(TestWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    {
        _factory = factory;
        _testOutputHelper = testOutputHelper;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid EnvironmentId, string ApiKey)> SeedEnvironmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Test Project" };
        db.Projects.Add(project); 
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

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
    public async Task ToggleFlag_ShouldUpdateDb_AndBroadcastViaSSE()
    {
        // Arrange
        var (projectId, envId, apiKey) = await SeedEnvironmentAsync();
        var flagKey = "signalr_test_flag";
        await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags", new CreateFlagRequest { Key = flagKey });

        var tcs = new TaskCompletionSource<GetFlagResponse>();

        var sseClient = _factory.CreateClient();
        sseClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        var cts = new CancellationTokenSource();
        var readyTcs = new TaskCompletionSource();
        _ = Task.Run(async () =>
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/stream");
                req.Headers.Add("Accept", "text/event-stream");
                var resp = await sseClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                resp.EnsureSuccessStatusCode();
                var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
                using var reader = new StreamReader(stream);

                readyTcs.TrySetResult();

                while (!cts.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cts.Token);
                    if (line == null) 
                    {
                        tcs.TrySetException(new Exception("SSE stream ended prematurely without receiving the event."));
                        break;
                    }
                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6);
                        var doc = System.Text.Json.JsonDocument.Parse(data);
                        if (doc.RootElement.TryGetProperty("EventName", out var evtName) && evtName.GetString() == "FlagUpdated")
                        {
                            if (doc.RootElement.TryGetProperty("Payload", out var payload))
                            {
                                var flag = System.Text.Json.JsonSerializer.Deserialize<GetFlagResponse>(payload.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                if (flag != null)
                                {
                                    tcs.TrySetResult(flag);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                readyTcs.TrySetException(ex);
            }
        }, cts.Token);

        await readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(200, cts.Token);

        // Act
        var toggleRequest = new ToggleFlagRequest { IsEnabled = true };
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/toggle", toggleRequest, cancellationToken: cts.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sseTask = tcs.Task;
        var completedTask = await Task.WhenAny(sseTask, Task.Delay(30000, cts.Token));

        completedTask.Should().Be(sseTask, "SSE event was not received within 30 seconds");

        var receivedFlag = await sseTask;
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
