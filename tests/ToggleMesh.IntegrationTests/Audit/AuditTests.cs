using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Create;
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
    public async Task CreateFlag_ShouldGenerateAuditLog()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Project" };
        db.Projects.Add(project);
        var env = new ProjectEnvironment { Name = "Audit Env", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        var request = new CreateFlagRequest { EnvironmentId = env.Id, Key = "audit_flag" };

        // Act
        await _client.PostAsJsonAsync("/api/flags", request);

        // Assert
        var logs = await db.AuditLogs
            .Where(x => x.EntityName == "FeatureFlag" && x.Action == "Added")
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();
        log.NewValues.Should().Contain("audit_flag");
        log.EnvironmentId.Should().Be(env.Id);
    }

    [Fact]
    public async Task UpdateFlag_ShouldGenerateAuditLog_WithOldAndNewValues()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Audit Project 2" };
        db.Projects.Add(project);
        var env = new ProjectEnvironment { Name = "Audit Env 2", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        await _client.PostAsJsonAsync("/api/flags", new CreateFlagRequest { EnvironmentId = env.Id, Key = "audit_update_flag" });
        
        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var toggleRequest = new { EnvironmentId = env.Id, Key = "audit_update_flag", IsEnabled = true };

        // Act
        await _client.PostAsJsonAsync("/api/flags/toggle", toggleRequest);

        // Assert
        var logs = await db.AuditLogs
            .Where(x => x.EntityName == "FeatureFlag" && x.Action == "Modified")
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();
        log.OldValues.Should().Contain("\"IsEnabled\": false");
        log.NewValues.Should().Contain("\"IsEnabled\": true");
    }
}