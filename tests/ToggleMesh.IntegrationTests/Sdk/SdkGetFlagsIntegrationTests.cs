using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;
using ToggleMesh.API.Features.Flags.SdkGetAll;

namespace ToggleMesh.IntegrationTests.Sdk;

[Collection("SharedEnv4")]
public class SdkGetFlagsIntegrationTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SdkGetFlagsIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetFlags_ShouldReturnMultivariateFlags()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "SDK Eval Test" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var env = new ProjectEnvironment { Name = "Production", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();

        var createKeyReq = new ToggleMesh.API.Features.Projects.CreateKey.CreateKeyRequest
        {
            Name = "SDK Test Key",
            Type = KeyType.Server
        };
        var createKeyResp = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/keys", createKeyReq);
        createKeyResp.EnsureSuccessStatusCode();
        var keyResult = await createKeyResp.Content.ReadFromJsonAsync<ToggleMesh.API.Features.Projects.CreateKey.CreateKeyResponse>();
        var publicKey = keyResult!.PlainKey;

        var varAId = Guid.CreateVersion7();
        var varBId = Guid.CreateVersion7();
        
        var createFlagReq = new
        {
            Key = "string_color",
            Type = FlagType.String,
            Variations = new[]
            {
                new { Id = varAId, Value = "red" },
                new { Id = varBId, Value = "blue" }
            }
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/flags", createFlagReq);
        response.EnsureSuccessStatusCode();

        var updateReq = new
        {
            IsEnabled = true,
            DefaultRollout = new[] { new { VariationId = varAId, Weight = 10000 } }
        };

        var putResponse = await _client.PutAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/flags/string_color", updateReq);
        putResponse.EnsureSuccessStatusCode();

        var jsonVarId = Guid.CreateVersion7();
        var createJsonReq = new
        {
            Key = "json_config",
            Type = FlagType.Json,
            Variations = new[]
            {
                new { Id = jsonVarId, Value = "{\"timeout\": 5000, \"retries\": 3}" }
            }
        };

        var respJson = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/flags", createJsonReq);
        respJson.EnsureSuccessStatusCode();

        var updateJsonReq = new
        {
            IsEnabled = true,
            DefaultRollout = new[] { new { VariationId = jsonVarId, Weight = 10000 } }
        };

        var putJsonResp = await _client.PutAsJsonAsync($"/api/v1/projects/{project.Id}/environments/{env.Id}/flags/json_config", updateJsonReq);
        putJsonResp.EnsureSuccessStatusCode();

        var sdkClient = _factory.CreateClient();
        sdkClient.DefaultRequestHeaders.Add("x-api-key", publicKey);
        
        var sdkFlagsResponse = await sdkClient.GetAsync("/api/v1/sdk/flags");
        sdkFlagsResponse.EnsureSuccessStatusCode();

        var flagsData = await sdkFlagsResponse.Content.ReadFromJsonAsync<SdkGetFlagsResponse>();
        flagsData.Should().NotBeNull();
        
        var colorFlag = flagsData.Flags.FirstOrDefault(f => f.Key == "string_color");
        colorFlag.Should().NotBeNull();
        colorFlag.Variations.Should().HaveCount(2);
        
        var jsonFlag = flagsData.Flags.FirstOrDefault(f => f.Key == "json_config");
        jsonFlag.Should().NotBeNull();
        jsonFlag.Variations.Should().HaveCount(1);
    }
}
