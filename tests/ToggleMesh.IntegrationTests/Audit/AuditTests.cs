using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Audit.Get;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Flags.Toggle;
using ToggleMesh.API.Features.Flags.Update;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Projects.AddMember;
using ToggleMesh.API.Features.Projects.CreateEnvironment;
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
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
            { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Audit Env", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var request = new CreateFlagRequest { Key = "audit_performed_by_flag" };

        // Act
        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/flags", request);

        // Assert
        var logs = await db.AuditLogs
            .Where(l => l.EntityName == "FeatureFlag" && l.Action == "Added")
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();

        log.PerformedByEmail.Should().Be(TestAuthHandler.TestUserEmail);
        log.EnvironmentId.Should().BeNull();
        log.EntityFriendlyName.Should().Be("audit_performed_by_flag");
    }

    [Fact]
    public async Task UpdateFlag_ShouldGenerateAuditLog_WithOldAndNewValues()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Project 2" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
            { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Audit Env 2", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/flags",
            new CreateFlagRequest { Key = "audit_update_flag" });

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var toggleRequest = new ToggleFlagRequest { IsEnabled = true };

        // Act
        await _client.PostAsJsonAsync(
            $"/api/v1/projects/{project.Id}/environments/{env.Id}/flags/audit_update_flag/toggle", toggleRequest);

        // Assert
        var logs = await db.AuditLogs
            .Where(x => x.EntityName == "FlagEnvironmentState" && x.Action == "Modified" && x.EnvironmentId == env.Id)
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();
        log.OldValues.Should().Contain("\"IsEnabled\": false");
        log.NewValues.Should().Contain("\"IsEnabled\": true");
        log.ProjectId.Should().Be(project.Id);
        log.EntityFriendlyName.Should().Be("audit_update_flag");
    }

    [Fact]
    public async Task UpdateFlag_AddRule_ShouldGenerateAuditLog_ForFlagRule_WithCorrectMetadata()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Rules Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
            { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Audit Rules Env", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/flags",
            new CreateFlagRequest { Key = "flag_with_rules" });

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var updateRequest = new UpdateFlagRequest
        {
            IsEnabled = true,
            Rules = [new RuleDto(0, "Email", "EndsWith", "@gmail.com")]
        };

        // Act
        await _client.PutAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/flags/flag_with_rules",
            updateRequest);

        // Assert
        var logs = await db.AuditLogs
            .Where(x => x.EntityName == "FlagRule" && x.Action == "Added")
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();

        log.EntityFriendlyName.Should().Be("flag_with_rules (Rule)");
        log.EnvironmentId.Should().Be(env.Id);
        log.ProjectId.Should().Be(project.Id);
        log.NewValues.Should().Contain("@gmail.com");
    }

    [Fact]
    public async Task AddProjectMember_ShouldGenerateAuditLog_WithEmailAsFriendlyName()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Member Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
            { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var targetUser = new ApplicationUser
            { Id = Guid.NewGuid(), Email = "new_member@test.com", UserName = "new_member@test.com" };
        db.Users.Add(targetUser);

        await db.SaveChangesAsync();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var request = new AddMemberRequest { Email = "new_member@test.com", Role = ProjectRole.Viewer };

        // Act
        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/members", request);

        // Assert
        var logs = await db.AuditLogs
            .Where(x => x.EntityName == "ProjectMember" && x.Action == "Added")
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();

        log.EntityFriendlyName.Should().Be("new_member@test.com");
        log.ProjectId.Should().Be(project.Id);
        log.EnvironmentId.Should().BeNull();
        log.NewValues.Should().Contain("\"Role\": \"Viewer\"");
    }

    [Fact]
    public async Task RemoveProjectMember_ShouldGenerateAuditLog_WithDeletedAction()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Remove Member" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
            { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var targetUser = new ApplicationUser
            { Id = Guid.NewGuid(), Email = "target@test.com", UserName = "target@test.com" };
        db.Users.Add(targetUser);
        db.ProjectMembers.Add(
            new ProjectMember { Project = project, UserId = targetUser.Id, Role = ProjectRole.Viewer });

        await db.SaveChangesAsync();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        // Act
        await _client.DeleteAsync($"/api/v1/projects/{project.Id}/members/{targetUser.Id}");

        // Assert
        var logs = await db.AuditLogs
            .Where(x => x.EntityName == "ProjectMember" && x.Action == "Deleted")
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();

        log.EntityFriendlyName.Should().Be("target@test.com");
        log.OldValues.Should().Contain("\"Role\": \"Viewer\"");
    }

    [Fact]
    public async Task CreateEnvironment_ShouldGenerateAuditLog()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Env Create" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
            { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        await db.SaveChangesAsync();
        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var request = new CreateEnvironmentRequest { Name = "Staging Env" };

        // Act
        await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments", request);

        // Assert
        var logs = await db.AuditLogs
            .Where(x => x.EntityName == "ProjectEnvironment" && x.Action == "Added")
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();

        log.EntityFriendlyName.Should().Be("Staging Env");
        log.ProjectId.Should().Be(project.Id);
    }
}