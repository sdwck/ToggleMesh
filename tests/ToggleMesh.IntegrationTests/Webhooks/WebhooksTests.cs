using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Webhooks;
using ToggleMesh.API.Features.Webhooks.CreateWebhook;
using ToggleMesh.API.Persistence;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Webhooks;

public class WebhooksTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public WebhooksTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateWebhook_WithValidData_ShouldSucceed_AndGenerateSecret()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Webhooks CRUD Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        await db.SaveChangesAsync();

        var request = new CreateWebhookRequest
        {
            Name = "Slack Integration",
            Url = "https://hooks.slack.com/services/123",
            Events = ["flag.created", "flag.updated"]
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/webhooks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Webhook>();
        result.Should().NotBeNull();
        result.Name.Should().Be("Slack Integration");
        result.Url.Should().Be("https://hooks.slack.com/services/123");
        result.SecretKey.Should().StartWith("whsec_");
    }

    [Fact]
    public async Task CreateWebhook_WithInvalidUrl_ShouldReturn400()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Webhooks Validation Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        await db.SaveChangesAsync();

        var request = new CreateWebhookRequest
        {
            Name = "Bad Webhook",
            Url = "ftp://not-http-url.com",
            Events = ["flag.created"]
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/webhooks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SavingChanges_ShouldPushEventToChannel_WhenFlagIsCreated()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Webhooks Integration Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });
        var env = new ProjectEnvironment { Name = "Prod_WH", Project = project };
        db.Environments.Add(env);
        await db.SaveChangesAsync();
        
        var channel = _factory.Services.GetRequiredService<Channel<WebhookEvent>>();
        while (channel.Reader.TryRead(out _)) { }

        var request = new CreateFlagRequest { Key = "webhook_trigger_flag" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/projects/{project.Id}/flags", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert
        channel.Reader.Count.Should().Be(1);
        channel.Reader.TryRead(out var evt).Should().BeTrue();
        evt!.EventName.Should().Be("flag.created");
        evt.FlagKey.Should().Be("webhook_trigger_flag");
        evt.ProjectId.Should().Be(project.Id);
    }
}