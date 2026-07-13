using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Metrics;

[Collection("SharedEnv1")]
public class SrmResetTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly AppDbContext _db;

    public SrmResetTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StartExperiment_ResetsSrmFields()
    {
        var org = new Organization { Name = "Test Org" };
        var project = new Project { Id = Guid.NewGuid(), Name = "Project", Organization = org };
        var env = new ProjectEnvironment { Id = Guid.NewGuid(), Name = "Env", ProjectId = project.Id };
        var flag = new FeatureFlag { Id = Guid.NewGuid(), Key = "test-flag", ProjectId = project.Id };
        
        var state = new FlagEnvironmentState
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = flag.Id,
            EnvironmentId = env.Id,
            IsExperimentActive = false,
            IsSrmAlertSent = true,
            SrmPValue = 0.0001
        };

        await _db.Projects.AddAsync(project);
        await _db.OrganizationMembers.AddAsync(new OrganizationMember { OrganizationId = org.Id, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = OrganizationRole.Admin });
        await _db.ProjectMembers.AddAsync(new ProjectMember { ProjectId = project.Id, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        await _db.Environments.AddAsync(env);
        await _db.FeatureFlags.AddAsync(flag);
        await _db.FlagEnvironmentStates.AddAsync(state);
        await _db.SaveChangesAsync();

        var req = new
        {
            mode = "standard",
            goalEvent = "purchase",
            optimizationType = 0,
            contextPartitionKeys = Array.Empty<string>()
        };

        var res = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/flags/{flag.Key}/experiments/start", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var updatedState = await _db.FlagEnvironmentStates.AsNoTracking().FirstAsync(s => s.Id == state.Id);
        
        Assert.True(updatedState.IsExperimentActive);
        Assert.False(updatedState.IsSrmAlertSent);
        Assert.Null(updatedState.SrmPValue);
    }
}
