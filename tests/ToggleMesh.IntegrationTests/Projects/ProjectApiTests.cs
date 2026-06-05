using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Projects.AddMember;
using ToggleMesh.API.Features.Projects.CreateProject;
using ToggleMesh.API.Features.Projects.GetMembers;
using ToggleMesh.API.Features.Projects.GetProject;
using ToggleMesh.API.Features.Projects.GetProjects;
using ToggleMesh.API.Persistence;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Projects;

public class ProjectApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public ProjectApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProject_ShouldReturn201_AndCreateDefaultEnvironments()
    {
        // Arrange
        var request = new CreateProjectRequest { Name = "New Integration Test Project" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/projects", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content.ReadFromJsonAsync<CreateProjectResponse>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Integration Test Project");
        result.Id.Should().NotBeEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = await db.Projects.Include(p => p.Environments).FirstOrDefaultAsync(p => p.Id == result.Id);
        
        project.Should().NotBeNull();
        project!.Environments.Should().HaveCount(2);
        project.Environments.Select(e => e.Name).Should().Contain(["Development", "Production"]);
    }

    [Fact]
    public async Task GetProjects_ShouldReturnListOfProjects()
    {
        // Arrange
        var request = new CreateProjectRequest { Name = "List Test Project" };
        await _client.PostAsJsonAsync("/api/v1/projects", request);

        // Act
        var response = await _client.GetAsync("/api/v1/projects");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<List<ProjectListDto>>();
        result.Should().NotBeNull();
        result!.Should().Contain(p => p.Name == "List Test Project");
    }

    [Fact]
    public async Task GetProject_ShouldReturnProjectDetails_WithEnvironments()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest { Name = "Details Test Project" });
        var createdProject = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>();

        // Act
        var response = await _client.GetAsync($"/api/v1/projects/{createdProject!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<GetProjectResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(createdProject.Id);
        result.Name.Should().Be("Details Test Project");
        result.Environments.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddMember_ShouldSucceed_WhenUserExists()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Member Test Project" };
        db.Projects.Add(project);

        var newUser = new API.Features.Auth.Models.ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "newuser@example.com",
            Email = "newuser@example.com"
        };
        db.Users.Add(newUser);
        await db.SaveChangesAsync();

        var request = new AddMemberRequest
        {
            Email = newUser.Email,
            Role = ProjectRole.Editor
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/members", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MemberDto>();
        result!.Email.Should().Be(newUser.Email);
        result.Role.Should().Be(ProjectRole.Editor);
    }

    [Fact]
    public async Task GetMembers_ShouldReturnList()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Get Members Test Project" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/v1/projects/{project.Id}/members");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var members = await response.Content.ReadFromJsonAsync<List<MemberDto>>();
        members.Should().NotBeNull();
    }
}
