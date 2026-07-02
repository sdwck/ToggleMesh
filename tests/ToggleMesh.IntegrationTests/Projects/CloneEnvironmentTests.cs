using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Projects;

[Collection("SharedEnv2")]
public class CloneEnvironmentTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public CloneEnvironmentTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CloneEnvironment_ShouldCopyAllFlagsAndRules_AndCleanTarget_AndNotifyViaSse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Clone Test Project" };
        db.Projects.Add(project); 
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var sourceEnv = new ProjectEnvironment { Name = "Source", Project = project };
        var targetEnv = new ProjectEnvironment { Name = "Target", Project = project };
        db.Environments.AddRange(sourceEnv, targetEnv);

        var plainKey = Guid.NewGuid().ToString("N");
        var keyHash = ApiKeyHasher.Hash(plainKey);
        var key = new EnvironmentKey
        {
            Environment = targetEnv,
            KeyHash = keyHash,
            KeyPreview = ApiKeyHasher.GeneratePreview(keyHash),
            CreatedOn = DateTime.UtcNow
        };
        db.EnvironmentKeys.Add(key);

        var flag = new FeatureFlag
        {
            Project = project,
            Key = "shared_feature"
        };

        var sourceState = new FlagEnvironmentState
        {
            FeatureFlag = flag,
            Environment = sourceEnv,
            IsEnabled = true,
            Rules = [
                new FlagRule { Attribute = "User", Operator = "Equals", Value = "Admin", GroupId = 0 }
            ]
        };

        var targetStateOld = new FlagEnvironmentState
        {
            FeatureFlag = flag,
            Environment = targetEnv,
            IsEnabled = false
        };

        db.FeatureFlags.Add(flag);
        db.FlagEnvironmentStates.AddRange(sourceState, targetStateOld);
        await db.SaveChangesAsync();

        var tcs = new TaskCompletionSource<bool>();
        var sseClient = _factory.CreateClient();
        sseClient.DefaultRequestHeaders.Add("x-api-key", plainKey);
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
                        var doc = System.Text.Json.JsonDocument.Parse(data);
                        if (doc.RootElement.TryGetProperty("EventName", out var evtName) && evtName.GetString() == "StateReloadRequired")
                        {
                            tcs.TrySetResult(true);
                            break;
                        }
                    }
                }
            }
            catch { /* ignore */ }
        }, cts.Token);

        await Task.Delay(500, cts.Token);

        // Act
        var response = await _client.PostAsync($"/api/v1/projects/{project.Id}/environments/{sourceEnv.Id}/clone-to/{targetEnv.Id}", null, cts.Token);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        db.ChangeTracker.Clear();

        var targetStates = await db.FlagEnvironmentStates
            .Where(x => x.EnvironmentId == targetEnv.Id)
            .Include(x => x.Rules)
            .ToListAsync(cancellationToken: cts.Token);

        targetStates.Should().HaveCount(1);
        targetStates[0].IsEnabled.Should().BeTrue();
        targetStates[0].Rules.Should().HaveCount(1);
        targetStates[0].Rules.First().Attribute.Should().Be("User");

        var sseTask = tcs.Task;
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), _factory.TimeProvider, cts.Token);
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(5.1));
        var completedTask = await Task.WhenAny(sseTask, timeoutTask);
        completedTask.Should().Be(sseTask, "SSE notification 'StateReloadRequired' was not received");
    }

    [Fact]
    public async Task CloneEnvironment_WithNonExistentTarget_ShouldReturn404()
    {
        // Arrange
        var projectId = Guid.CreateVersion7();
        var sourceEnvId = Guid.CreateVersion7();
        var targetEnvId = Guid.CreateVersion7();

        // Act
        var response = await _client.PostAsync($"/api/projects/{projectId}/environments/{sourceEnvId}/clone-to/{targetEnvId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
