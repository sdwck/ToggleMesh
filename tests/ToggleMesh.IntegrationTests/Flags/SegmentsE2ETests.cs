using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Segments.Create;
using ToggleMesh.API.Features.Segments.Domain;
using ToggleMesh.API.Features.Segments.Update;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Flags;

[Collection("SharedEnv2")]
public class SegmentsE2ETests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public SegmentsE2ETests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid EnvironmentId, string ApiKey)> SeedEnvironmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Segments Test Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Role = ProjectRole.Owner
        });

        var environment = new ProjectEnvironment { Name = "Production", Project = project };
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
    public async Task CreateSegment_Should_Return201_And_BeEvaluatedCorrectly()
    {
        // Arrange
        var (projectId, envId, _) = await SeedEnvironmentAsync();
        var request = new CreateSegmentRequest
        {
            Name = "Beta Testers",
            Description = "Internal testers group",
            Rules = [new RuleInput(0, "Email", "EndsWith", "@example.com")]
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/environments/{envId}/segments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<SegmentDto>();
        result.Should().NotBeNull();
        result.Name.Should().Be("Beta Testers");
        result.Rules.Should().ContainSingle(r => r.Attribute == "Email" && r.Operator == "EndsWith" && r.Value == "@example.com");
    }

    [Fact]
    public async Task UpdateSegment_Should_InvalidateCache_And_BroadcastSignalR_For_All_Flags_Using_It()
    {
        // Arrange
        var (projectId, envId, apiKey) = await SeedEnvironmentAsync();

        var createSegResponse = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/environments/{envId}/segments",
            new CreateSegmentRequest
            {
                Name = "Segment To Edit",
                Description = "Temp segment",
                Rules = [new RuleInput(0, "Country", "Equals", "US")]
            });
        createSegResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var segment = await createSegResponse.Content.ReadFromJsonAsync<SegmentDto>();
        segment.Should().NotBeNull();

        var flagKey1 = "flag_using_segment_1";
        var flagKey2 = "flag_using_segment_2";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var flag1 = new FeatureFlag { ProjectId = projectId, Key = flagKey1 };
            var flag2 = new FeatureFlag { ProjectId = projectId, Key = flagKey2 };
            db.FeatureFlags.AddRange(flag1, flag2);

            var state1 = new FlagEnvironmentState
            {
                EnvironmentId = envId,
                FeatureFlag = flag1,
                IsEnabled = true,
                Rules = new List<FlagRule> { new() { GroupId = 0, Attribute = "", Operator = "InSegment", Value = segment.Id.ToString() } }
            };
            var state2 = new FlagEnvironmentState
            {
                EnvironmentId = envId,
                FeatureFlag = flag2,
                IsEnabled = true,
                Rules = new List<FlagRule> { new() { GroupId = 0, Attribute = "", Operator = "InSegment", Value = segment.Id.ToString() } }
            };
            db.FlagEnvironmentStates.AddRange(state1, state2);
            await db.SaveChangesAsync();
        }

        var tcs1 = new TaskCompletionSource<GetFlagResponse>();
        var tcs2 = new TaskCompletionSource<GetFlagResponse>();

        var sseClient = _factory.CreateClient();
        sseClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/stream");
                req.Headers.Add("Accept", "text/event-stream");
                var resp = await sseClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
                using var reader = new StreamReader(stream);
                while (!cts.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cts.Token);
                    if (line?.StartsWith("data: ") == true)
                    {
                        var data = line.Substring(6);
                        var doc = JsonDocument.Parse(data);
                        if (doc.RootElement.TryGetProperty("EventName", out var evtName) && evtName.GetString() == "FlagUpdated")
                        {
                            if (doc.RootElement.TryGetProperty("Payload", out var payload))
                            {
                                var flag = JsonSerializer.Deserialize<GetFlagResponse>(payload.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                if (flag != null)
                                {
                                    if (flag.Key == flagKey1) tcs1.TrySetResult(flag);
                                    if (flag.Key == flagKey2) tcs2.TrySetResult(flag);
                                }
                            }
                        }
                    }
                }
            }
            catch { /* Ignore */ }
        }, cts.Token);

        await Task.Delay(500, cts.Token);

        // Act
        var updateRequest = new UpdateSegmentRequest
        {
            Name = "Updated Segment Name",
            Description = "Updated description",
            Rules = [new(0, "Country", "Equals", "CA")]
        };
        var updateSegResponse = await _client.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/environments/{envId}/segments/{segment.Id}",
            updateRequest, cancellationToken: cts.Token);
        updateSegResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert
        var signalRTask1 = tcs1.Task;
        var signalRTask2 = tcs2.Task;
        var completed = await Task.WhenAny(Task.WhenAll(signalRTask1, signalRTask2), Task.Delay(10000, cts.Token));

        completed.Should().NotBe(Task.Delay(10000, cts.Token), "SignalR update events should be broadcast for all flags referencing the segment");

        var broadcast1 = await signalRTask1;
        var broadcast2 = await signalRTask2;

        broadcast1.Key.Should().Be(flagKey1);
        broadcast2.Key.Should().Be(flagKey2);

        var redis = _factory.Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>().GetDatabase();
        var cachedVal1 = await redis.StringGetAsync($"flags:{envId}:{flagKey1}");
        cachedVal1.HasValue.Should().BeTrue();
        var cachedFlag = JsonSerializer.Deserialize<GetFlagResponse>((string)cachedVal1!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        cachedFlag.Should().NotBeNull();
        cachedFlag.Rules.Should().ContainSingle(r => r.Operator == "InSegment" && r.Value == segment.Id.ToString());

        await cts.CancelAsync();
    }
}
