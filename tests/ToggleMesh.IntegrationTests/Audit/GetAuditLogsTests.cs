using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Audit.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.Common.Pagination;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Audit;

[Collection("SharedEnv4")]
public class GetAuditLogsTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public GetAuditLogsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAuditLogs_Forbidden_WhenNotProjectMember()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var otherOrg = new Organization { Id = Guid.NewGuid(), Name = "Other Forbidden Org" };
        db.Organizations.Add(otherOrg);

        var project = new Project { Name = "Forbidden Audit Logs Project", OrganizationId = otherOrg.Id };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/v1/audit-logs?projectId={project.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAuditLogs_FilterByProjectAndEnvironment()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Logs Project 1" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Role = ProjectRole.Owner
        });

        var env1 = new ProjectEnvironment { Name = "Env 1", Project = project };
        var env2 = new ProjectEnvironment { Name = "Env 2", Project = project };
        db.Environments.AddRange(env1, env2);
        await db.SaveChangesAsync();

        var logProj = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EnvironmentId = null,
            EntityName = "Project",
            EntityFriendlyName = "Audit Logs Project 1",
            EntityId = project.Id.ToString(),
            Action = "Added",
            Timestamp = DateTime.UtcNow,
            PerformedByEmail = TestAuthHandler.TestUserEmail,
            OldValues = "{}",
            NewValues = "{}"
        };

        var logEnv1 = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EnvironmentId = env1.Id,
            EntityName = "FeatureFlag",
            EntityFriendlyName = "flag_env1",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Modified",
            Timestamp = DateTime.UtcNow,
            PerformedByEmail = TestAuthHandler.TestUserEmail,
            OldValues = "{}",
            NewValues = "{}"
        };

        var logEnv2 = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EnvironmentId = env2.Id,
            EntityName = "FeatureFlag",
            EntityFriendlyName = "flag_env2",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Modified",
            Timestamp = DateTime.UtcNow,
            PerformedByEmail = TestAuthHandler.TestUserEmail,
            OldValues = "{}",
            NewValues = "{}"
        };

        db.AuditLogs.AddRange(logProj, logEnv1, logEnv2);
        await db.SaveChangesAsync();

        // Act
        var resProj = await _client.GetAsync($"/api/v1/audit-logs?projectId={project.Id}");
        var resEnv1 = await _client.GetAsync($"/api/v1/audit-logs?environmentId={env1.Id}");

        // Assert
        resProj.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtoProj = await resProj.Content.ReadFromJsonAsync<CursorPagedResponse<AuditLogDto>>();
        dtoProj.Should().NotBeNull();
        dtoProj.Items.Should().ContainSingle(x => x.Id == logProj.Id);

        resEnv1.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtoEnv1 = await resEnv1.Content.ReadFromJsonAsync<CursorPagedResponse<AuditLogDto>>();
        dtoEnv1.Should().NotBeNull();
        dtoEnv1.Items.Should().ContainSingle(x => x.Id == logEnv1.Id);
    }

    [Fact]
    public async Task GetAuditLogs_FilterByActionAndEntityName()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Logs Project 2" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Role = ProjectRole.Owner
        });
        await db.SaveChangesAsync();

        var log1 = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EntityName = "FeatureFlag",
            EntityFriendlyName = "flag_one",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Added",
            Timestamp = DateTime.UtcNow,
            PerformedByEmail = TestAuthHandler.TestUserEmail,
            OldValues = "{}",
            NewValues = "{}"
        };

        var log2 = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EntityName = "ProjectEnvironment",
            EntityFriendlyName = "env_one",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Deleted",
            Timestamp = DateTime.UtcNow,
            PerformedByEmail = TestAuthHandler.TestUserEmail,
            OldValues = "{}",
            NewValues = "{}"
        };

        db.AuditLogs.AddRange(log1, log2);
        await db.SaveChangesAsync();

        // Act
        var res1 = await _client.GetAsync($"/api/v1/audit-logs?projectId={project.Id}&action=added");
        var res2 = await _client.GetAsync($"/api/v1/audit-logs?projectId={project.Id}&entityName=ProjectEnvironment");

        // Assert
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto1 = await res1.Content.ReadFromJsonAsync<CursorPagedResponse<AuditLogDto>>();
        dto1.Should().NotBeNull();
        dto1.Items.Should().ContainSingle(x => x.Id == log1.Id);

        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto2 = await res2.Content.ReadFromJsonAsync<CursorPagedResponse<AuditLogDto>>();
        dto2.Should().NotBeNull();
        dto2.Items.Should().ContainSingle(x => x.Id == log2.Id);
    }

    [Fact]
    public async Task GetAuditLogs_FilterBySearch()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Logs Project 3" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Role = ProjectRole.Owner
        });
        await db.SaveChangesAsync();

        var entityIdMatch = Guid.NewGuid().ToString();

        var logFriendly = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EntityName = "FeatureFlag",
            EntityFriendlyName = "target_friendly_name",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Added",
            Timestamp = DateTime.UtcNow,
            PerformedByEmail = TestAuthHandler.TestUserEmail,
            OldValues = "{}",
            NewValues = "{}"
        };

        var logEntityId = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EntityName = "FeatureFlag",
            EntityFriendlyName = "other_name",
            EntityId = entityIdMatch,
            Action = "Added",
            Timestamp = DateTime.UtcNow,
            PerformedByEmail = TestAuthHandler.TestUserEmail,
            OldValues = "{}",
            NewValues = "{}"
        };

        var logEmail = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EntityName = "FeatureFlag",
            EntityFriendlyName = "another_name",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Added",
            Timestamp = DateTime.UtcNow,
            PerformedByEmail = "special_actor@test.com",
            OldValues = "{}",
            NewValues = "{}"
        };

        db.AuditLogs.AddRange(logFriendly, logEntityId, logEmail);
        await db.SaveChangesAsync();

        // Act
        var resFriendly = await _client.GetAsync($"/api/v1/audit-logs?projectId={project.Id}&search=target_friendly");
        var resEntityId = await _client.GetAsync($"/api/v1/audit-logs?projectId={project.Id}&search={entityIdMatch}");
        var resEmail = await _client.GetAsync($"/api/v1/audit-logs?projectId={project.Id}&search=special_actor");

        // Assert
        resFriendly.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtoFriendly = await resFriendly.Content.ReadFromJsonAsync<CursorPagedResponse<AuditLogDto>>();
        dtoFriendly.Should().NotBeNull();
        dtoFriendly.Items.Should().ContainSingle(x => x.Id == logFriendly.Id);

        resEntityId.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtoEntityId = await resEntityId.Content.ReadFromJsonAsync<CursorPagedResponse<AuditLogDto>>();
        dtoEntityId.Should().NotBeNull();
        dtoEntityId.Items.Should().ContainSingle(x => x.Id == logEntityId.Id);

        resEmail.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtoEmail = await resEmail.Content.ReadFromJsonAsync<CursorPagedResponse<AuditLogDto>>();
        dtoEmail.Should().NotBeNull();
        dtoEmail.Items.Should().ContainSingle(x => x.Id == logEmail.Id);
    }

    [Fact]
    public async Task GetAuditLogs_FilterByDateRange()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Logs Project 4" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Role = ProjectRole.Owner
        });
        await db.SaveChangesAsync();

        var baseTime = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);

        var logOld = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EntityName = "FeatureFlag",
            EntityFriendlyName = "old_log",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Added",
            Timestamp = baseTime.AddDays(-5),
            PerformedByEmail = TestAuthHandler.TestUserEmail,
            OldValues = "{}",
            NewValues = "{}"
        };

        var logTarget = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EntityName = "FeatureFlag",
            EntityFriendlyName = "target_log",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Added",
            Timestamp = baseTime,
            PerformedByEmail = TestAuthHandler.TestUserEmail,
            OldValues = "{}",
            NewValues = "{}"
        };

        var logNew = new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            EntityName = "FeatureFlag",
            EntityFriendlyName = "new_log",
            EntityId = Guid.NewGuid().ToString(),
            Action = "Added",
            Timestamp = baseTime.AddDays(5),
            PerformedByEmail = TestAuthHandler.TestUserEmail,
            OldValues = "{}",
            NewValues = "{}"
        };

        db.AuditLogs.AddRange(logOld, logTarget, logNew);
        await db.SaveChangesAsync();

        var fromStr = baseTime.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var toStr = baseTime.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Act
        var response = await _client.GetAsync($"/api/v1/audit-logs?projectId={project.Id}&dateFrom={fromStr}&dateTo={toStr}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CursorPagedResponse<AuditLogDto>>();
        dto.Should().NotBeNull();
        dto.Items.Should().ContainSingle(x => x.Id == logTarget.Id);
    }

    [Fact]
    public async Task GetAuditLogs_SortAndCursorPagination()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Logs Project 5" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Role = ProjectRole.Owner
        });
        await db.SaveChangesAsync();

        var logs = new List<AuditLog>();
        for (int i = 1; i <= 5; i++)
        {
            logs.Add(new AuditLog
            {
                Id = Guid.CreateVersion7(),
                ProjectId = project.Id,
                EntityName = "FeatureFlag",
                EntityFriendlyName = $"log_{i}",
                EntityId = Guid.NewGuid().ToString(),
                Action = "Added",
                Timestamp = DateTime.UtcNow.AddMinutes(i),
                PerformedByEmail = TestAuthHandler.TestUserEmail,
                OldValues = "{}",
                NewValues = "{}"
            });
        }

        db.AuditLogs.AddRange(logs);
        await db.SaveChangesAsync();

        var sortedLogs = logs.OrderByDescending(l => l.Timestamp).ThenByDescending(l => l.Id).ToList();

        // Act
        var resFirstPage = await _client.GetAsync($"/api/v1/audit-logs?projectId={project.Id}&pageSize=2&sortOrder=desc");
        var dtoFirst = await resFirstPage.Content.ReadFromJsonAsync<CursorPagedResponse<AuditLogDto>>();

        var resSecondPage = await _client.GetAsync($"/api/v1/audit-logs?projectId={project.Id}&pageSize=2&sortOrder=desc&cursor={dtoFirst!.NextCursor}");
        var dtoSecond = await resSecondPage.Content.ReadFromJsonAsync<CursorPagedResponse<AuditLogDto>>();

        // Assert
        dtoFirst.Should().NotBeNull();
        dtoFirst.Items.Should().HaveCount(2);
        var firstPageItems = dtoFirst.Items.ToList();
        firstPageItems[0].Id.Should().Be(sortedLogs[0].Id);
        firstPageItems[1].Id.Should().Be(sortedLogs[1].Id);
        dtoFirst.HasNextPage.Should().BeTrue();
        dtoFirst.NextCursor.Should().NotBeNullOrEmpty();

        dtoSecond.Should().NotBeNull();
        dtoSecond.Items.Should().HaveCount(2);
        var secondPageItems = dtoSecond.Items.ToList();
        secondPageItems[0].Id.Should().Be(sortedLogs[2].Id);
        secondPageItems[1].Id.Should().Be(sortedLogs[3].Id);
        dtoSecond.HasNextPage.Should().BeTrue();
    }
}
