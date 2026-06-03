using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Flags.Get;
using ToggleMesh.API.Features.Flags.Update;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Persistence;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Flags;

public class FlagRulesApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public FlagRulesApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid EnvironmentId, string ApiKey)> SeedEnvironmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Test Project - Rules" };
        db.Projects.Add(project);

        var environment = new ProjectEnvironment { Name = "Staging", Project = project };
        db.Environments.Add(environment);

        var key = new EnvironmentKey
        {
            Environment = environment,
            ApiKey = Guid.NewGuid().ToString("N"),
            CreatedOn = DateTime.UtcNow
        };
        db.EnvironmentKeys.Add(key);

        await db.SaveChangesAsync();
        return (environment.Id, key.ApiKey);
    }

    [Fact]
    public async Task CreateFlag_WithRules_ShouldReturnRulesInResponse()
    {
        // Arrange
        var (envId, _) = await SeedEnvironmentAsync();
        var request = new CreateFlagRequest 
        { 
            EnvironmentId = envId, 
            Key = "rule_flag_1",
            Rules =
            [
                new RuleDto(0, "Region", "InList", "EU,US"),
                new RuleDto(0, "Plan", "Equals", "Premium")
            ]
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/flags", request);

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
        var (envId, _) = await SeedEnvironmentAsync();
        
        var createRequest = new CreateFlagRequest 
        { 
            EnvironmentId = envId, 
            Key = "rule_flag_update",
            Rules = [new RuleDto(0, "Age", "GreaterThan", "18")]
        };
        await _client.PostAsJsonAsync("/api/flags", createRequest);

        var updateRequest = new UpdateFlagRequest
        {
            EnvironmentId = envId,
            Key = "rule_flag_update",
            IsEnabled = true,
            Rules =
            [
                new RuleDto(0, "Country", "Equals", "CA"),
                new RuleDto(0, "Device", "StartsWith", "Mobile")
            ]
        };

        // Act
        var updateResponse = await _client.PutAsJsonAsync("/api/flags", updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await updateResponse.Content.ReadFromJsonAsync<GetFlagResponse>();
        result.Should().NotBeNull();
        result.IsEnabled.Should().BeTrue();
        result.Rules.Should().HaveCount(2);
        result.Rules.Should().NotContain(r => r.Attribute == "Age");
        result.Rules.Should().Contain(r => r.Attribute == "Country" && r.Value == "CA");
    }
}