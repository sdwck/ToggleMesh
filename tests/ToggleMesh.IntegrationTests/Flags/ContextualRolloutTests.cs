using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Flags.SetContextualRollout;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;
using Xunit;

namespace ToggleMesh.IntegrationTests.Flags;

public class ContextualRolloutTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContextualRolloutTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeleteContextualRollout_ShouldNotDeleteHistoricalMetrics()
    {
        // Arrange
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var testProject = new Project
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Test Project",
            CreatedAt = DateTime.UtcNow
        };
        db.Projects.Add(testProject);

        var testEnv = new ProjectEnvironment
        {
            Id = Guid.NewGuid(),
            ProjectId = testProject.Id,
            Name = "Test Env",
            CreatedAt = DateTime.UtcNow
        };
        db.Environments.Add(testEnv);
        
        var flag = new ToggleMesh.API.Features.Flags.Domain.FeatureFlag
        {
            Id = Guid.NewGuid(),
            ProjectId = testProject.Id,
            Key = "rollout-test-flag",
            Name = "Rollout Test",
            CreatedAt = DateTime.UtcNow
        };
        db.FeatureFlags.Add(flag);

        var state = new ToggleMesh.API.Features.Flags.Domain.FlagEnvironmentState
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = flag.Id,
            EnvironmentId = testEnv.Id,
            IsEnabled = true,
            ContextualRollouts = new List<ContextualRollout> { new ContextualRollout { Id = Guid.NewGuid(), ContextSlice = "country=US", RolloutPercentage = 50 } }
        };
        db.FlagEnvironmentStates.Add(state);

        db.ContextualExperimentMetrics.Add(new ContextualExperimentMetric
        {
            Id = Guid.NewGuid(),
            EnvironmentId = testEnv.Id,
            FlagKey = flag.Key,
            EventName = "test-event",
            ContextSlice = "country=US",
            TotalExposures = 100,
            TotalConversions = 10,
            LastCalculatedAt = DateTime.UtcNow.AddDays(-1)
        });

        await db.SaveChangesAsync();

        // Act
        var request = new ToggleMesh.API.Features.Analytics.DeleteContextualRollout.DeleteContextualRolloutRequest
        {
            ContextSlice = "country=US"
        };
        
        var requestMsg = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/projects/{testProject.Id}/environments/{testEnv.Id}/flags/{flag.Key}/contextual-rollouts")
        {
            Content = JsonContent.Create(request)
        };
        var response = await client.SendAsync(requestMsg);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedState = await db.FlagEnvironmentStates.AsNoTracking().Include(x => x.ContextualRollouts).FirstAsync(x => x.Id == state.Id);
        updatedState.ContextualRollouts.Should().NotContain(x => x.ContextSlice == "country=US");

        var metricExists = await db.ContextualExperimentMetrics.AnyAsync(x => x.FlagKey == flag.Key && x.ContextSlice == "country=US");
        metricExists.Should().BeTrue("Because deleting a rollout configuration should not delete historical metrics.");
    }
}
