using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Toggle;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.AddMember;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Flags;

[Collection("SharedEnv2")]
public class RbacIntegrationTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public RbacIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid EnvironmentId, string FlagKey, SeededUsers Users)> SeedRbacScenarioAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var orgA = new Organization { Name = "Org A" };
        db.Organizations.Add(orgA);

        var project = new Project { Name = "Project A", Organization = orgA };
        db.Projects.Add(project);

        var env = new ProjectEnvironment { Name = "Env A", Project = project };
        db.Environments.Add(env);

        var flag = new FeatureFlag { Project = project, Key = "rbac_flag_1" };
        db.FeatureFlags.Add(flag);

        var state = new FlagEnvironmentState { Environment = env, FeatureFlag = flag, IsEnabled = false };
        db.FlagEnvironmentStates.Add(state);

        var suffix = Guid.NewGuid().ToString("N");
        var ownerUser = CreateUser(db, $"owner_{suffix}@example.com");
        var adminUser = CreateUser(db, $"admin_{suffix}@example.com");
        var editorUser = CreateUser(db, $"editor_{suffix}@example.com");
        var viewerUser = CreateUser(db, $"viewer_{suffix}@example.com");
        var orgAdminUser = CreateUser(db, $"orgadmin_{suffix}@example.com");

        var otherOrg = new Organization { Name = $"Other Org {suffix}" };
        db.Organizations.Add(otherOrg);
        var otherOrgUser = CreateUser(db, $"other_{suffix}@example.com");

        db.OrganizationMembers.Add(new OrganizationMember { Organization = orgA, UserId = ownerUser.Id, Role = OrganizationRole.Member });
        db.OrganizationMembers.Add(new OrganizationMember { Organization = orgA, UserId = adminUser.Id, Role = OrganizationRole.Member });
        db.OrganizationMembers.Add(new OrganizationMember { Organization = orgA, UserId = editorUser.Id, Role = OrganizationRole.Member });
        db.OrganizationMembers.Add(new OrganizationMember { Organization = orgA, UserId = viewerUser.Id, Role = OrganizationRole.Member });
        db.OrganizationMembers.Add(new OrganizationMember { Organization = orgA, UserId = orgAdminUser.Id, Role = OrganizationRole.Admin });

        db.OrganizationMembers.Add(new OrganizationMember { Organization = otherOrg, UserId = otherOrgUser.Id, Role = OrganizationRole.Member });

        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = ownerUser.Id, Role = ProjectRole.Owner });
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = adminUser.Id, Role = ProjectRole.Admin });
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = editorUser.Id, Role = ProjectRole.Editor });
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = viewerUser.Id, Role = ProjectRole.Viewer });

        await db.SaveChangesAsync();

        return (project.Id, env.Id, flag.Key, new SeededUsers(
            ownerUser.Id, adminUser.Id, editorUser.Id, viewerUser.Id, orgAdminUser.Id, otherOrgUser.Id
        ));
    }

    private ApplicationUser CreateUser(AppDbContext db, string email)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NormalizedUserName = email.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant()
        };
        db.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task Viewer_Cannot_ToggleFlag_ShouldReturn403()
    {
        // Arrange
        var (projectId, envId, flagKey, users) = await SeedRbacScenarioAsync();

        // Act
        var toggleUrl = $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/toggle";
        var toggleReq = new HttpRequestMessage(HttpMethod.Post, toggleUrl)
        {
            Content = JsonContent.Create(new ToggleFlagRequest { IsEnabled = true })
        };
        toggleReq.Headers.Add("x-test-user-id", users.ViewerId.ToString());

        var response = await _client.SendAsync(toggleReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editor_Cannot_AddMember_ShouldReturn403()
    {
        // Arrange
        var (projectId, _, _, users) = await SeedRbacScenarioAsync();

        // Act
        var addMemberUrl = $"/api/v1/projects/{projectId}/members";
        var addMemberReq = new HttpRequestMessage(HttpMethod.Post, addMemberUrl)
        {
            Content = JsonContent.Create(new AddMemberRequest { Email = "new_member@example.com", Role = ProjectRole.Viewer })
        };
        addMemberReq.Headers.Add("x-test-user-id", users.EditorId.ToString());

        var response = await _client.SendAsync(addMemberReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_With_EnvAdminOverride_Can_ToggleFlag_ButCannot_DeleteProject()
    {
        // Arrange
        var (projectId, envId, flagKey, users) = await SeedRbacScenarioAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var viewerProjMember = await db.ProjectMembers
                .FirstAsync(pm => pm.ProjectId == projectId && pm.UserId == users.ViewerId);

            db.MemberEnvironmentRoles.Add(new MemberEnvironmentRole
            {
                ProjectMember = viewerProjMember,
                EnvironmentId = envId,
                Role = ProjectRole.Admin
            });
            await db.SaveChangesAsync();
        }

        var toggleUrl = $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/toggle";
        var toggleReq = new HttpRequestMessage(HttpMethod.Post, toggleUrl)
        {
            Content = JsonContent.Create(new ToggleFlagRequest { IsEnabled = true })
        };
        toggleReq.Headers.Add("x-test-user-id", users.ViewerId.ToString());

        var toggleResponse = await _client.SendAsync(toggleReq);
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Viewer has Admin override on this environment");

        var deleteUrl = $"/api/v1/projects/{projectId}";
        var deleteReq = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
        deleteReq.Headers.Add("x-test-user-id", users.ViewerId.ToString());

        var deleteResponse = await _client.SendAsync(deleteReq);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "Viewer cannot delete projects even with environment-level overrides");
    }

    [Fact]
    public async Task User_From_Different_Org_Cannot_Read_Flags_ShouldReturn403()
    {
        // Arrange
        var (projectId, envId, flagKey, users) = await SeedRbacScenarioAsync();

        // Act
        var readUrl = $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}";
        var readReq = new HttpRequestMessage(HttpMethod.Get, readUrl);
        readReq.Headers.Add("x-test-user-id", users.OtherOrgUserId.ToString());

        var response = await _client.SendAsync(readReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_Can_ReadFlags_ShouldReturn200()
    {
        // Arrange
        var (projectId, envId, flagKey, users) = await SeedRbacScenarioAsync();

        // Act
        var readUrl = $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}";
        var readReq = new HttpRequestMessage(HttpMethod.Get, readUrl);
        readReq.Headers.Add("x-test-user-id", users.ViewerId.ToString());

        var response = await _client.SendAsync(readReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OrganizationAdmin_Can_PerformAllOperations_EvenWithoutProjectMembership()
    {
        // Arrange
        var (projectId, envId, flagKey, users) = await SeedRbacScenarioAsync();

        // Act
        var toggleUrl = $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/toggle";
        var toggleReq = new HttpRequestMessage(HttpMethod.Post, toggleUrl)
        {
            Content = JsonContent.Create(new ToggleFlagRequest { IsEnabled = true })
        };
        toggleReq.Headers.Add("x-test-user-id", users.OrgAdminId.ToString());

        var response = await _client.SendAsync(toggleReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record SeededUsers(
        Guid OwnerId,
        Guid AdminId,
        Guid EditorId,
        Guid ViewerId,
        Guid OrgAdminId,
        Guid OtherOrgUserId);
}
