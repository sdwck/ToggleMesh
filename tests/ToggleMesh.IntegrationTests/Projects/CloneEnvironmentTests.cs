using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.API.Persistence;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Projects;

public class CloneEnvironmentTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public CloneEnvironmentTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CloneEnvironment_ShouldCopyAllFlagsAndRules_AndCleanTarget_AndNotifyViaSignalR()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Clone Test Project" };
        db.Projects.Add(project); db.ProjectMembers.Add(new ToggleMesh.API.Features.Projects.ProjectMember { Project = project, UserId = Guid.Parse(ToggleMesh.IntegrationTests.Infrastructure.TestAuthHandler.TestUserId), Role = ToggleMesh.API.Features.Projects.ProjectRole.Owner });

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
            Rules = 
            [
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
        var hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost/api/v1/hubs/toggle", options => 
            {
                options.Headers.Add("x-api-key", plainKey);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        hubConnection.On("StateReloadRequired", () => tcs.SetResult(true));
        await hubConnection.StartAsync();

        // Act
        var response = await _client.PostAsync($"/api/v1/projects/{project.Id}/environments/{sourceEnv.Id}/clone-to/{targetEnv.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        db.ChangeTracker.Clear();

        var targetStates = await db.FlagEnvironmentStates
            .Where(x => x.EnvironmentId == targetEnv.Id)
            .Include(x => x.Rules)
            .ToListAsync();

        targetStates.Should().HaveCount(1);
        targetStates[0].IsEnabled.Should().BeTrue();
        targetStates[0].Rules.Should().HaveCount(1);
        targetStates[0].Rules.First().Attribute.Should().Be("User");

        var signalRTask = tcs.Task;
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), _factory.TimeProvider);
        _factory.TimeProvider.Advance(TimeSpan.FromSeconds(5.1));
        var completedTask = await Task.WhenAny(signalRTask, timeoutTask);
        completedTask.Should().Be(signalRTask, "SignalR notification 'StateReloadRequired' was not received");
    }

    [Fact]
    public async Task CloneEnvironment_WithNonExistentTarget_ShouldReturn404()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var sourceEnvId = Guid.NewGuid();
        var targetEnvId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/projects/{projectId}/environments/{sourceEnvId}/clone-to/{targetEnvId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
