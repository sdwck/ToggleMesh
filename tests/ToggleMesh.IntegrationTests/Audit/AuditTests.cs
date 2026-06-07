using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Audit.Get;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Toggle;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Persistence;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Audit;

public class AuditTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AuditTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateFlag_ShouldGenerateAuditLog_WithCorrectPerformedBy()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Project" };
        db.Projects.Add(project); db.ProjectMembers.Add(new ToggleMesh.API.Features.Projects.ProjectMember { Project = project, UserId = Guid.Parse(ToggleMesh.IntegrationTests.Infrastructure.TestAuthHandler.TestUserId), Role = ToggleMesh.API.Features.Projects.ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Audit Env", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        var request = new CreateFlagRequest { Key = "audit_performed_by_flag" };

        // Act
        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/flags", request);

        // Assert
        var searchResponse = await _client.GetAsync($"/api/v1/audit-logs?ProjectId={project.Id}");
        searchResponse.EnsureSuccessStatusCode();
        var result = await searchResponse.Content.ReadFromJsonAsync<GetAuditLogsResponse>();

        result!.Items.Should().Contain(l => l.EntityName == "FeatureFlag" && l.Action == "Added");
        var log = result.Items.First(l => l.EntityName == "FeatureFlag" && l.Action == "Added");

        log.PerformedBy.Should().Be(TestAuthHandler.TestUserEmail);
        log.EnvironmentId.Should().BeNull();
    }

    [Fact]
    public async Task CreateFlag_ShouldGenerateAuditLog()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Project" };
        db.Projects.Add(project); db.ProjectMembers.Add(new ToggleMesh.API.Features.Projects.ProjectMember { Project = project, UserId = Guid.Parse(ToggleMesh.IntegrationTests.Infrastructure.TestAuthHandler.TestUserId), Role = ToggleMesh.API.Features.Projects.ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Audit Env", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        var request = new CreateFlagRequest { Key = "audit_flag" };

        // Act
        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/flags", request);

        // Assert
        var logs = await db.AuditLogs
            .Where(x => x.EntityName == "FlagEnvironmentState" && x.Action == "Added" && x.EnvironmentId == env.Id)
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();
        log.EnvironmentId.Should().Be(env.Id);
    }

    [Fact]
    public async Task UpdateFlag_ShouldGenerateAuditLog_WithOldAndNewValues()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Project 2" };
        db.Projects.Add(project); db.ProjectMembers.Add(new ToggleMesh.API.Features.Projects.ProjectMember { Project = project, UserId = Guid.Parse(ToggleMesh.IntegrationTests.Infrastructure.TestAuthHandler.TestUserId), Role = ToggleMesh.API.Features.Projects.ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Audit Env 2", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/flags", new CreateFlagRequest { Key = "audit_update_flag" });

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var toggleRequest = new ToggleFlagRequest { IsEnabled = true };        

        // Act
        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/flags/audit_update_flag/toggle", toggleRequest);

        // Assert
        var logs = await db.AuditLogs
            .Where(x => x.EntityName == "FlagEnvironmentState" && x.Action == "Modified" && x.EnvironmentId == env.Id)
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();
        log.OldValues.Should().Contain("\"IsEnabled\": false");
        log.NewValues.Should().Contain("\"IsEnabled\": true");
    }
}
