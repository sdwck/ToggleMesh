using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Toggle;
using ToggleMesh.API.Features.Flags.Update;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.AddMember;
using ToggleMesh.API.Features.Projects.CreateEnvironment;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Audit;

[Collection("SharedEnv4")]
public class AuditTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
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
        { Id = Guid.CreateVersion7(), Email = "new_member@test.com", UserName = "new_member@test.com" };
        db.Users.Add(targetUser);

        db.OrganizationMembers.Add(new OrganizationMember
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = targetUser.Id,
            Role = OrganizationRole.Member
        });

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
        { Id = Guid.CreateVersion7(), Email = "target@test.com", UserName = "target@test.com" };
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

    [Fact]
    public async Task UpdateProject_ShouldGenerateAuditLog_WithProjectFriendlyName()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Old Project Name" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        await db.SaveChangesAsync();

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var request = new { Name = "New Project Name" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/projects/{project.Id}", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        var logs = await db.AuditLogs
            .Where(x => x.EntityName == "Project" && x.Action == "Modified")
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();
        log.EntityFriendlyName.Should().Be("New Project Name");
        log.ProjectId.Should().Be(project.Id);
    }

    [Fact]
    public async Task SoftDeleteAndRestore_ShouldGenerateCorrectAuditAction()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Soft Delete Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        await db.SaveChangesAsync();

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        // Act
        await _client.DeleteAsync($"/api/v1/projects/{project.Id}");

        db.ChangeTracker.Clear();
        var deletedLogs = await db.AuditLogs
            .Where(x => x.EntityName == "Project" && x.Action == "Deleted")
            .ToListAsync();

        var projFromDb = await db.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == project.Id);
        projFromDb.IsDeleted = false;
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var restoredLogs = await db.AuditLogs
            .Where(x => x.EntityName == "Project" && x.Action == "Restored")
            .ToListAsync();

        // Assert
        deletedLogs.Should().HaveCount(1);
        restoredLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task RefreshToken_ShouldNotBeAudited()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "refresh_audit@test.com",
            Email = "refresh_audit@test.com"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!int.TryParse(
                configuration["Auth:RefreshTokenLifetimeDays"],
                out var tokenLifetime))
            tokenLifetime = 7;

        var token = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Token = "some_refresh_token",
            Expires = DateTime.UtcNow.AddDays(tokenLifetime),
            Created = DateTime.UtcNow
        };

        // Act
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();

        token.Token = "updated_refresh_token";
        await db.SaveChangesAsync();

        db.RefreshTokens.Remove(token);
        await db.SaveChangesAsync();

        // Assert
        var logs = await db.AuditLogs.ToListAsync();
        logs.Should().BeEmpty();
    }
}