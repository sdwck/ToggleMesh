using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Integrations.Domain;
using ToggleMesh.API.Features.Organizations.Domain;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace ToggleMesh.IntegrationTests.Integrations;

[Collection("SharedEnv1")]
public class IntegrationsCrudTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private AppDbContext _db;

    public IntegrationsCrudTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CanCreateReadUpdateDeleteIntegration()
    {
        var org = new ToggleMesh.API.Features.Organizations.Domain.Organization { Name = "Test Org" };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Organization = org
        };
        await _db.Projects.AddAsync(project);
        await _db.OrganizationMembers.AddAsync(new OrganizationMember { OrganizationId = org.Id, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = OrganizationRole.Admin });
        await _db.ProjectMembers.AddAsync(new ProjectMember { ProjectId = project.Id, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        await _db.SaveChangesAsync();

        var createReq = new
        {
            projectId = project.Id,
            provider = IntegrationProvider.Slack,
            name = "Test Slack",
            webhookUrl = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXXXXXX",
            events = new[] { "flag.updated" },
            environmentIds = Array.Empty<Guid>()
        };

        var createRes = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/integrations", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var createdDto = await createRes.Content.ReadFromJsonAsync<IntegrationDto>();
        Assert.NotNull(createdDto);
        Assert.Equal("Test Slack", createdDto.Name);

        var listRes = await _client.GetAsync($"/api/v1/projects/{project.Id}/integrations");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        
        var list = await listRes.Content.ReadFromJsonAsync<List<IntegrationDto>>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal("*** HIDDEN ***", list[0].WebhookUrl);

        var updateReq = new
        {
            id = createdDto.Id,
            projectId = project.Id,
            name = "Updated Slack",
            events = new[] { "flag.created", "flag.updated" },
            environmentIds = Array.Empty<Guid>(),
            isActive = false
        };

        var updateRes = await _client.PutAsJsonAsync($"/api/v1/projects/{project.Id}/integrations/{createdDto.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updatedDto = await updateRes.Content.ReadFromJsonAsync<IntegrationDto>();
        Assert.NotNull(updatedDto);
        Assert.Equal("Updated Slack", updatedDto.Name);
        Assert.False(updatedDto.IsActive);
        Assert.Equal(2, updatedDto.Events.Length);

        var deleteRes = await _client.DeleteAsync($"/api/v1/projects/{project.Id}/integrations/{createdDto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var listAfterDeleteRes = await _client.GetAsync($"/api/v1/projects/{project.Id}/integrations");
        var listAfterDelete = await listAfterDeleteRes.Content.ReadFromJsonAsync<List<IntegrationDto>>();
        Assert.Empty(listAfterDelete);
    }
}
