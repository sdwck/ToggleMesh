using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.BackgroundServices;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Workers;

[Collection("SharedEnv3")]
public class ScheduledChangesWorkerTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private ScheduledChangesWorker _worker = null!;

    public ScheduledChangesWorkerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        var scope = _factory.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ScheduledChangesWorker>>();
        _worker = new ScheduledChangesWorker(_factory.Services, logger);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ApplicationUser> EnsureUserExistsAsync(AppDbContext db, Guid userId)
    {
        var existing = await db.Users.FindAsync(userId);
        if (existing != null) return existing;

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"worker_user_{userId:N}@test.com",
            Email = $"worker_user_{userId:N}@test.com",
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task ExecuteWorkerMethodAsync()
    {
        var method = typeof(ScheduledChangesWorker).GetMethod("ProcessPendingAndScheduledChangesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(_worker, [CancellationToken.None])!;
    }

    [Fact]
    public async Task Worker_ShouldExpireUnapprovedChanges_WhenExecuteAtHasPassed()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await EnsureUserExistsAsync(db, Guid.CreateVersion7());
        var projectId = Guid.CreateVersion7();
        var envId = Guid.CreateVersion7();
        var flagId = Guid.CreateVersion7();

        var project = new Project { Id = projectId, Name = "Test Project" };
        var env = new ProjectEnvironment { Id = envId, ProjectId = projectId, Name = "Prod" };
        var flag = new FeatureFlag { Id = flagId, ProjectId = projectId, Key = "expire-flag", Name = "Expire Flag" };

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = user.Id,
            PatchInstructionsJson = "{}",
            Status = PendingFlagChangeStatus.PendingReview,
            ExecuteAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        db.Projects.Add(project);
        db.Environments.Add(env);
        db.FeatureFlags.Add(flag);
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        // Act
        await ExecuteWorkerMethodAsync();

        // Assert
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.PendingFlagChanges.FindAsync(change.Id);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(PendingFlagChangeStatus.Expired);
    }

    [Fact]
    public async Task Worker_ShouldExecuteScheduledChange_AndApplyPatch()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await EnsureUserExistsAsync(db, Guid.CreateVersion7());
        var projectId = Guid.CreateVersion7();
        var envId = Guid.CreateVersion7();
        var flagId = Guid.CreateVersion7();
        var varA = Guid.CreateVersion7();
        var varB = Guid.CreateVersion7();

        var project = new Project { Id = projectId, Name = "Test Project" };
        var env = new ProjectEnvironment { Id = envId, ProjectId = projectId, Name = "Prod" };
        var flag = new FeatureFlag
        {
            Id = flagId,
            ProjectId = projectId,
            Key = "scheduled-flag",
            Name = "Scheduled Flag",
            Variations = new List<FlagVariation>
            {
                new FlagVariation { Id = varA, FeatureFlagId = flagId, Key = "off", Value = "false", Name = "False" },
                new FlagVariation { Id = varB, FeatureFlagId = flagId, Key = "on", Value = "true", Name = "True" }
            }
        };

        var state = new FlagEnvironmentState
        {
            Id = Guid.CreateVersion7(),
            FeatureFlagId = flagId,
            EnvironmentId = envId,
            IsEnabled = true,
            OffVariationId = varA
        };

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = user.Id,
            PatchInstructionsJson = $"{{\"offVariationId\":\"{varB}\"}}",
            Status = PendingFlagChangeStatus.Scheduled,
            ExecuteAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        db.Projects.Add(project);
        db.Environments.Add(env);
        db.FeatureFlags.Add(flag);
        db.FlagEnvironmentStates.Add(state);
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        // Act
        await ExecuteWorkerMethodAsync();

        // Assert
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedChange = await verifyDb.PendingFlagChanges.FindAsync(change.Id);
        updatedChange!.Status.Should().Be(PendingFlagChangeStatus.Executed);

        var updatedState = await verifyDb.FlagEnvironmentStates.FirstOrDefaultAsync(s => s.FeatureFlagId == flagId && s.EnvironmentId == envId);
        updatedState!.OffVariationId.Should().Be(varB);
    }

    [Fact]
    public async Task Worker_ShouldHandleInvalidJson_AndMarkStatusAsConflictFailed()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await EnsureUserExistsAsync(db, Guid.CreateVersion7());
        var projectId = Guid.CreateVersion7();
        var envId = Guid.CreateVersion7();
        var flagId = Guid.CreateVersion7();

        var project = new Project { Id = projectId, Name = "Test Project" };
        var env = new ProjectEnvironment { Id = envId, ProjectId = projectId, Name = "Prod" };
        var flag = new FeatureFlag { Id = flagId, ProjectId = projectId, Key = "corrupt-flag", Name = "Corrupt Flag" };
        var state = new FlagEnvironmentState { Id = Guid.CreateVersion7(), FeatureFlagId = flagId, EnvironmentId = envId };

        var change = new PendingFlagChange
        {
            Id = Guid.CreateVersion7(),
            FlagId = flagId,
            EnvironmentId = envId,
            RequestedByUserId = user.Id,
            PatchInstructionsJson = "INVALID_JSON_CORRUPTED",
            Status = PendingFlagChangeStatus.Scheduled,
            ExecuteAt = null
        };

        db.Projects.Add(project);
        db.Environments.Add(env);
        db.FeatureFlags.Add(flag);
        db.FlagEnvironmentStates.Add(state);
        db.PendingFlagChanges.Add(change);
        await db.SaveChangesAsync();

        // Act
        await ExecuteWorkerMethodAsync();

        // Assert
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedChange = await verifyDb.PendingFlagChanges.FindAsync(change.Id);
        updatedChange!.Status.Should().Be(PendingFlagChangeStatus.ConflictFailed);
    }
}
