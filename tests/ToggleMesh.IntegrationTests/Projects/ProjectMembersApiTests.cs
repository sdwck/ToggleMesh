using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Auth.Models;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Projects.UpdateMember;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Projects;

[Collection("Sequential")]
public class ProjectMembersApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProjectMembersApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid TargetUserId)> SeedProjectAndMembersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToggleMesh.API.Persistence.AppDbContext>();

        var project = new Project { Name = "Members Test Project" };
        
        var ownerUser = await db.Users.SingleAsync(u => u.Email == TestAuthHandler.TestUserEmail);
        
        var targetUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "targetuser@example.com",
            UserName = "targetuser@example.com"
        };
        db.Users.Add(targetUser);
        
        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = ownerUser.Id,
            Role = ProjectRole.Owner
        });
        
        db.ProjectMembers.Add(new ProjectMember
        {
            Project = project,
            UserId = targetUser.Id,
            Role = ProjectRole.Viewer
        });

        db.Projects.Add(project);
        await db.SaveChangesAsync();

        return (project.Id, targetUser.Id);
    }

    [Fact]
    public async Task UpdateMember_ShouldUpdateRole()
    {
        // Arrange
        var (projectId, targetUserId) = await SeedProjectAndMembersAsync();
        var updateRequest = new UpdateMemberRequest { Role = ProjectRole.Editor };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/projects/{projectId}/members/{targetUserId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToggleMesh.API.Persistence.AppDbContext>();
        var updatedMember = await db.ProjectMembers.SingleAsync(m => m.ProjectId == projectId && m.UserId == targetUserId);
        updatedMember.Role.Should().Be(ProjectRole.Editor);
    }

    [Fact]
    public async Task UpdateMember_OnSelf_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToggleMesh.API.Persistence.AppDbContext>();
        
        var ownerUser = await db.Users.SingleAsync(u => u.Email == TestAuthHandler.TestUserEmail);
        var project = new Project { Name = "Self Update Test" };
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = ownerUser.Id, Role = ProjectRole.Owner });
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var updateRequest = new UpdateMemberRequest { Role = ProjectRole.Viewer };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/projects/{project.Id}/members/{ownerUser.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveMember_ShouldDeleteFromDb()
    {
        // Arrange
        var (projectId, targetUserId) = await SeedProjectAndMembersAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/projects/{projectId}/members/{targetUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToggleMesh.API.Persistence.AppDbContext>();
        var memberExists = await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == targetUserId);
        memberExists.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMember_OnSelf_ShouldReturnBadRequest()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToggleMesh.API.Persistence.AppDbContext>();
        
        var ownerUser = await db.Users.SingleAsync(u => u.Email == TestAuthHandler.TestUserEmail);
        var project = new Project { Name = "Self Delete Test" };
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = ownerUser.Id, Role = ProjectRole.Owner });
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/projects/{project.Id}/members/{ownerUser.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
