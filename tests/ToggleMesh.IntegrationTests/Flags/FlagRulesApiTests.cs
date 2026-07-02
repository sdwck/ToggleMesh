using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Flags.Update;
using ToggleMesh.API.Features.Flags.UpdateMetadata;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.API.Infrastructure.Security;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Flags;

[Collection("SharedEnv3")]
public class FlagRulesApiTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public FlagRulesApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid ProjectId, Guid EnvironmentId, string ApiKey)> SeedEnvironmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Test Project - Rules" };
        db.Projects.Add(project); db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var environment = new ProjectEnvironment { Name = "Staging", Project = project };
        db.Environments.Add(environment);

        var plainKey = Guid.NewGuid().ToString("N");
        var keyHash = ApiKeyHasher.Hash(plainKey);
        var key = new EnvironmentKey
        {
            Environment = environment,
            KeyHash = keyHash,
            KeyPreview = ApiKeyHasher.GeneratePreview(keyHash),
            CreatedOn = DateTime.UtcNow
        };
        db.EnvironmentKeys.Add(key);

        await db.SaveChangesAsync();
        return (project.Id, environment.Id, plainKey);
    }

    [Fact]
    public async Task CreateFlag_WithRules_ShouldReturnRulesInResponse()
    {
        // Arrange
        var (projectId, _, _) = await SeedEnvironmentAsync();
        var request = new CreateFlagRequest
        {
            Key = "rule_flag_1",
            Rules =
            [
                new RuleDto(0, "Region", "InList", "EU,US"),
                new RuleDto(0, "Plan", "Equals", "Premium")
            ]
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<GetFlagResponse>();
        result.Should().NotBeNull();
        result.Key.Should().Be("rule_flag_1");
        result.Rules.Should().HaveCount(2);
        result.Rules.Should().Contain(r => r.Attribute == "Region" && r.Value == "EU,US");
    }

    [Fact]
    public async Task UpdateFlag_WithNewRules_ShouldReplaceExistingRules()
    {
        // Arrange
        var (projectId, envId, _) = await SeedEnvironmentAsync();

        var createRequest = new CreateFlagRequest
        {
            Key = "rule_flag_update",
            Rules = [new RuleDto(0, "Age", "GreaterThan", "18")]
        };
        await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags", createRequest);

        var updateRequest = new UpdateFlagRequest
        {
            IsEnabled = true,
            Rules =
            [
                new RuleDto(0, "Country", "Equals", "CA"),
                new RuleDto(0, "Device", "StartsWith", "Mobile")
            ]
        };

        // Act
        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/projects/{projectId}/environments/{envId}/flags/rule_flag_update", updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await updateResponse.Content.ReadFromJsonAsync<GetFlagResponse>();
        result.Should().NotBeNull();
        result.IsEnabled.Should().BeTrue();
        result.Rules.Should().HaveCount(2);
        result.Rules.Should().NotContain(r => r.Attribute == "Age");
        result.Rules.Should().Contain(r => r.Attribute == "Country" && r.Value == "CA");
    }

    [Fact]
    public async Task CreateFlag_WithTags_ShouldReturnTagsInResponse()
    {
        // Arrange
        var (projectId, _, _) = await SeedEnvironmentAsync();
        var request = new CreateFlagRequest
        {
            Key = "tagged_flag_test",
            Tags = ["billing", "api", "v2"]
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<GetFlagResponse>();
        result.Should().NotBeNull();
        result.Tags.Should().HaveCount(3);
        result.Tags.Should().Contain(["billing", "api", "v2"]);
    }

    [Fact]
    public async Task UpdateFlag_WithNewTags_ShouldReplaceExistingTags()
    {
        // Arrange
        var (projectId, envId, _) = await SeedEnvironmentAsync();

        var createRequest = new CreateFlagRequest
        {
            Key = "tag_update_flag",
            Tags = ["old"]
        };
        await _client.PostAsJsonAsync($"/api/v1/projects/{projectId}/flags", createRequest);

        var updateRequest = new UpdateFlagMetadataRequest
        {
            Name = "tag_update_flag",
            Description = "Updated metadata",
            Tags = ["new1", "new2"]
        };

        // Act
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/flags/tag_update_flag/metadata",
            updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync(
            $"/api/v1/projects/{projectId}/environments/{envId}/flags/tag_update_flag");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await getResponse.Content.ReadFromJsonAsync<GetFlagResponse>();
        result.Should().NotBeNull();
        result.Tags.Should().HaveCount(2);
        result.Tags.Should().NotContain("old");
        result.Tags.Should().Contain(["new1", "new2"]);
    }
}
