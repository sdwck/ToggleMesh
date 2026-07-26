using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Analytics.Domain;
using ToggleMesh.API.Features.Analytics.Services;
using ToggleMesh.API.Features.Privacy.PurgeIdentity;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security.Authorization.Models;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Analytics;

[Collection("SharedEnv2")]
public class PurgeIdentityEndpointTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public PurgeIdentityEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PurgeIdentity_ShouldDeleteExposuresAndTracksFromDatabase()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IIdentityHasher>();

        var testOrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var ownerUser = await db.Users.SingleAsync(u => u.Email == TestAuthHandler.TestUserEmail);

        var project = new Project 
        { 
            Id = Guid.NewGuid(), 
            OrganizationId = testOrgId, 
            Name = "Purge Project" 
        };
        db.Projects.Add(project);

        db.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = ownerUser.Id,
            Role = ProjectRole.Owner
        });

        var envId = Guid.NewGuid();
        var environment = new ProjectEnvironment 
        { 
            Id = envId, 
            ProjectId = project.Id, 
            Name = "Production"
        };
        db.Environments.Add(environment);

        var rawUser = "user_to_delete_test";
        var hashedUser = hasher.HashIdentity(rawUser);

        db.AnalyticsExposures.Add(new AnalyticsExposure
        {
            Id = Guid.NewGuid(),
            EnvironmentId = envId,
            Identity = hashedUser,
            FlagKey = "flag1",
            VariationId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow
        });

        db.AnalyticsTracks.Add(new AnalyticsTrack
        {
            Id = Guid.NewGuid(),
            EnvironmentId = envId,
            Identity = hashedUser,
            EventName = "click",
            Timestamp = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Project-Id");
        _client.DefaultRequestHeaders.Add("X-Project-Id", project.Id.ToString());

        var request = new PurgeIdentityRequest
        {
            Identity = rawUser,
            EnvironmentId = envId
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/privacy/purge-identity", request);

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Status: {response.StatusCode}, Body: {body}");

        var result = await response.Content.ReadFromJsonAsync<PurgeIdentityResponse>();
        result.Should().NotBeNull();
        result!.ExposuresPurged.Should().Be(1);
        result.TracksPurged.Should().Be(1);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var remainingExposures = await verifyDb.AnalyticsExposures.Where(e => e.EnvironmentId == envId).ToListAsync();
        var remainingTracks = await verifyDb.AnalyticsTracks.Where(t => t.EnvironmentId == envId).ToListAsync();

        remainingExposures.Should().BeEmpty();
        remainingTracks.Should().BeEmpty();
    }
}
