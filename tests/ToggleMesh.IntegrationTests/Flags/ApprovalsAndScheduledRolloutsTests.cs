using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.API.Features.Flags.CreatePendingChange;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.ReviewPendingChange;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;
using ToggleMesh.API.Features.Organizations.Domain;

namespace ToggleMesh.IntegrationTests.Flags;

[Collection("SharedEnv2")]
public class ApprovalsAndScheduledRolloutsTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApprovalsAndScheduledRolloutsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ApplicationUser> EnsureUserExistsAsync(AppDbContext db, Guid userId)
    {
        var existing = await db.Users.FindAsync(userId);
        if (existing != null) return existing;

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId:N}@test.com",
            Email = $"user_{userId:N}@test.com",
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<(Guid ProjectId, Guid EnvironmentId, Guid FlagId, string FlagKey)> SeedFlagAndEnvironmentAsync(
        ProjectRole userRole = ProjectRole.Owner,
        bool isProtected = false,
        bool isExperimentActive = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var testUserId = Guid.Parse(TestAuthHandler.TestUserId);
        await EnsureUserExistsAsync(db, testUserId);

        var projectId = Guid.CreateVersion7();
        var envId = Guid.CreateVersion7();
        var flagId = Guid.CreateVersion7();
        var flagKey = $"flag-{Guid.NewGuid():N}";

        var project = new Project { Id = projectId, Name = "Approvals Test Project" };
        var member = new ProjectMember
        {
            ProjectId = projectId,
            UserId = testUserId,
            Role = userRole
        };

        var environment = new ProjectEnvironment
        {
            Id = envId,
            ProjectId = projectId,
            Name = "Production",
            RequireApprovals = true,
            RequiredApprovalsCount = 1
        };

        var flag = new FeatureFlag
        {
            Id = flagId,
            ProjectId = projectId,
            Key = flagKey,
            Name = "Test Flag",
            IsProtected = isProtected
        };

        var state = new FlagEnvironmentState
        {
            Id = Guid.CreateVersion7(),
            FeatureFlagId = flagId,
            EnvironmentId = envId,
            IsEnabled = true,
            IsExperimentActive = isExperimentActive
        };

        db.Projects.Add(project);
        db.ProjectMembers.Add(member);
        db.Environments.Add(environment);
        db.FeatureFlags.Add(flag);
        db.FlagEnvironmentStates.Add(state);

        await db.SaveChangesAsync();
        return (projectId, envId, flagId, flagKey);
    }

    [Fact]
    public async Task CreatePendingChange_WhenExperimentIsActive_ShouldReturn400BadRequest()
    {
        // Arrange
        var (projectId, envId, _, flagKey) = await SeedFlagAndEnvironmentAsync(isExperimentActive: true);

        var req = new CreatePendingChangeRequest(
            PatchInstructionsJson: "{\"offVariationId\":\"00000000-0000-0000-0000-000000000000\"}",
            ExecuteAt: null,
            Comment: "Trying to update during experiment"
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes", req);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePendingChange_WithFutureExecuteAt_ShouldCreatePendingReviewChange()
    {
        // Arrange
        var (projectId, envId, _, flagKey) = await SeedFlagAndEnvironmentAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var otherAdmin = await EnsureUserExistsAsync(db, Guid.CreateVersion7());
            db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = otherAdmin.Id, Role = ProjectRole.Admin });
            await db.SaveChangesAsync();
        }

        var futureDate = DateTimeOffset.UtcNow.AddHours(2);
        var req = new CreatePendingChangeRequest(
            PatchInstructionsJson: "{\"offVariationId\":\"00000000-0000-0000-0000-000000000001\"}",
            ExecuteAt: futureDate,
            Comment: "Scheduled change"
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes", req);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var change = await db2.PendingFlagChanges.FirstOrDefaultAsync(c => c.EnvironmentId == envId);
        change.Should().NotBeNull();
        change.Status.Should().Be(PendingFlagChangeStatus.PendingReview);
        change.Comment.Should().Be("Scheduled change");
    }

    [Fact]
    public async Task CreatePendingChange_WhenNotEnoughApprovers_ShouldReturn400BadRequest()
    {
        // Arrange
        var (projectId, envId, _, flagKey) = await SeedFlagAndEnvironmentAsync();

        var req = new CreatePendingChangeRequest(
            PatchInstructionsJson: "{\"isEnabled\":true}",
            ExecuteAt: null,
            Comment: "Deadlocked change"
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes", req);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Not enough eligible approvers");
    }

    [Fact]
    public async Task ReviewPendingChange_WhenUserIsAuthor_ShouldReturn400BadRequest()
    {
        // Arrange
        var (projectId, envId, flagId, flagKey) = await SeedFlagAndEnvironmentAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = Guid.Parse(TestAuthHandler.TestUserId),
            PatchInstructionsJson = "{}",
            Status = PendingFlagChangeStatus.PendingReview
        };
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        var reviewReq = new ReviewPendingChangeRequest(ReviewAction.Approve);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes/{change.Id}/review", reviewReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReviewPendingChange_WhenUserIsEditor_ShouldReturn403Forbidden()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var editorUser = await EnsureUserExistsAsync(db, Guid.CreateVersion7());
        var authorUser = await EnsureUserExistsAsync(db, Guid.CreateVersion7());

        var (projectId, envId, flagId, flagKey) = await SeedFlagAndEnvironmentAsync(userRole: ProjectRole.Owner);

        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = editorUser.Id,
            Role = ProjectRole.Editor
        });
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = editorUser.Id,
            Role = OrganizationRole.Member
        });

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = authorUser.Id,
            PatchInstructionsJson = "{}",
            Status = PendingFlagChangeStatus.PendingReview
        };
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        var reviewReq = new ReviewPendingChangeRequest(ReviewAction.Approve);

        using var editorClient = _factory.CreateClient();
        editorClient.DefaultRequestHeaders.Add("x-test-user-id", editorUser.Id.ToString());

        // Act
        var response = await editorClient.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes/{change.Id}/review", reviewReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReviewPendingChange_WhenUserIsAdmin_ShouldApproveAndSetScheduledStatus()
    {
        // Arrange
        var (projectId, envId, flagId, flagKey) = await SeedFlagAndEnvironmentAsync(userRole: ProjectRole.Admin);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var author = await EnsureUserExistsAsync(db, Guid.CreateVersion7());

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = author.Id,
            PatchInstructionsJson = "{\"isEnabled\":true}",
            Status = PendingFlagChangeStatus.PendingReview,
            ExecuteAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        var reviewReq = new ReviewPendingChangeRequest(ReviewAction.Approve);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes/{change.Id}/review", reviewReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedChange = await verifyDb.PendingFlagChanges.FindAsync(change.Id);
        updatedChange.Should().NotBeNull();
        updatedChange.Status.Should().Be(PendingFlagChangeStatus.Scheduled);
        updatedChange.ApprovedByUserIds.Should().Contain(Guid.Parse(TestAuthHandler.TestUserId));
    }

    [Fact]
    public async Task ReviewPendingChange_WhenActionIsReject_ShouldSetRejectedStatus()
    {
        // Arrange
        var (projectId, envId, flagId, flagKey) = await SeedFlagAndEnvironmentAsync(userRole: ProjectRole.Owner);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var author = await EnsureUserExistsAsync(db, Guid.CreateVersion7());

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = author.Id,
            PatchInstructionsJson = "{}",
            Status = PendingFlagChangeStatus.PendingReview
        };
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        var reviewReq = new ReviewPendingChangeRequest(ReviewAction.Reject);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes/{change.Id}/review", reviewReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedChange = await verifyDb.PendingFlagChanges.FindAsync(change.Id);
        updatedChange!.Status.Should().Be(PendingFlagChangeStatus.Rejected);
    }
    [Fact]
    public async Task ExecuteScheduledChange_WhenUserIsEditorAndNotAuthor_ShouldReturn403Forbidden()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var editorUser = await EnsureUserExistsAsync(db, Guid.CreateVersion7());
        var authorUser = await EnsureUserExistsAsync(db, Guid.CreateVersion7());

        var (projectId, envId, flagId, flagKey) = await SeedFlagAndEnvironmentAsync(userRole: ProjectRole.Owner);

        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = editorUser.Id,
            Role = ProjectRole.Editor
        });
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = editorUser.Id,
            Role = OrganizationRole.Member
        });

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = authorUser.Id,
            PatchInstructionsJson = "{}",
            Status = PendingFlagChangeStatus.Scheduled,
            ExecuteAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        using var editorClient = _factory.CreateClient();
        editorClient.DefaultRequestHeaders.Add("x-test-user-id", editorUser.Id.ToString());

        // Act
        var response = await editorClient.PostAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes/{change.Id}/execute-now", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExecuteScheduledChange_WhenUserIsEditorAndIsAuthor_ShouldReturn200Ok()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var editorUser = await EnsureUserExistsAsync(db, Guid.CreateVersion7());

        var (projectId, envId, flagId, flagKey) = await SeedFlagAndEnvironmentAsync(userRole: ProjectRole.Owner);

        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = editorUser.Id,
            Role = ProjectRole.Editor
        });
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = editorUser.Id,
            Role = OrganizationRole.Member
        });

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = editorUser.Id,
            PatchInstructionsJson = "{}",
            Status = PendingFlagChangeStatus.Scheduled,
            ExecuteAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        using var editorClient = _factory.CreateClient();
        editorClient.DefaultRequestHeaders.Add("x-test-user-id", editorUser.Id.ToString());

        // Act
        var response = await editorClient.PostAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes/{change.Id}/execute-now", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelScheduledChange_WhenUserIsEditorAndNotAuthor_ShouldReturn403Forbidden()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var editorUser = await EnsureUserExistsAsync(db, Guid.CreateVersion7());
        var authorUser = await EnsureUserExistsAsync(db, Guid.CreateVersion7());

        var (projectId, envId, flagId, flagKey) = await SeedFlagAndEnvironmentAsync(userRole: ProjectRole.Owner);

        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = editorUser.Id,
            Role = ProjectRole.Editor
        });
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = editorUser.Id,
            Role = OrganizationRole.Member
        });

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = authorUser.Id,
            PatchInstructionsJson = "{}",
            Status = PendingFlagChangeStatus.Scheduled,
            ExecuteAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        using var editorClient = _factory.CreateClient();
        editorClient.DefaultRequestHeaders.Add("x-test-user-id", editorUser.Id.ToString());

        // Act
        var response = await editorClient.PostAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes/{change.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelScheduledChange_WhenUserIsEditorAndIsAuthor_ShouldReturn200Ok()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var editorUser = await EnsureUserExistsAsync(db, Guid.CreateVersion7());

        var (projectId, envId, flagId, flagKey) = await SeedFlagAndEnvironmentAsync(userRole: ProjectRole.Owner);

        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = editorUser.Id,
            Role = ProjectRole.Editor
        });
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = editorUser.Id,
            Role = OrganizationRole.Member
        });

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = editorUser.Id,
            PatchInstructionsJson = "{}",
            Status = PendingFlagChangeStatus.Scheduled,
            ExecuteAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        using var editorClient = _factory.CreateClient();
        editorClient.DefaultRequestHeaders.Add("x-test-user-id", editorUser.Id.ToString());

        // Act
        var response = await editorClient.PostAsync($"/api/v1/projects/{projectId}/flags/{flagKey}/environments/{envId}/changes/{change.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
