using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Features.Webhooks;
using ToggleMesh.API.Features.Webhooks.CreateWebhook;
using ToggleMesh.API.Features.Webhooks.UpdateWebhook;
using ToggleMesh.API.Features.Webhooks.UpdateWebhookStatus;
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

    [Fact]
    public async Task GetWebhooks_ShouldReturnList_ForProject()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Get Webhooks Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var hook1 = new Webhook
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Name = "Hook 1",
            Url = "https://example.com/hook1",
            Events = ["flag.created"],
            SecretKey = "whsec_123"
        };
        db.Webhooks.Add(hook1);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/v1/projects/{project.Id}/webhooks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<Webhook>>();
        result.Should().NotBeNull();
        result.Should().ContainSingle(h => h.Id == hook1.Id);
    }

    [Fact]
    public async Task UpdateWebhook_WithValidData_ShouldModifyWebhook()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Update Webhooks Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var hook = new Webhook
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Name = "Old Hook Name",
            Url = "https://example.com/old",
            Events = ["flag.created"],
            SecretKey = "whsec_123"
        };
        db.Webhooks.Add(hook);
        await db.SaveChangesAsync();

        var request = new UpdateWebhookRequest
        {
            Name = "New Hook Name",
            Url = "https://example.com/new",
            Events = ["flag.updated"]
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/projects/{project.Id}/webhooks/{hook.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Webhook>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Hook Name");
        result.Url.Should().Be("https://example.com/new");
        result.Events.Should().ContainSingle(e => e == "flag.updated");
    }

    [Fact]
    public async Task DeleteWebhook_ShouldRemoveWebhook()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Delete Webhook Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var hook = new Webhook
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Name = "Hook to Delete",
            Url = "https://example.com/delete",
            Events = ["flag.created"],
            SecretKey = "whsec_123"
        };
        db.Webhooks.Add(hook);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/projects/{project.Id}/webhooks/{hook.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var hookInDb = await db.Webhooks.FirstOrDefaultAsync(h => h.Id == hook.Id);
        hookInDb.Should().BeNull();
    }

    [Fact]
    public async Task GetWebhookDeliveries_ShouldReturnPagedResponse()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Deliveries Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var hook = new Webhook
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Name = "Hook 1",
            Url = "https://example.com/hook",
            Events = ["flag.created"],
            SecretKey = "whsec_123"
        };
        db.Webhooks.Add(hook);

        var delivery = new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            WebhookId = hook.Id,
            EventName = "flag.created",
            Payload = "{}",
            Status = WebhookDeliveryStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.WebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/v1/projects/{project.Id}/webhooks/{hook.Id}/deliveries");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Common.Pagination.PagedResponse<WebhookDelivery>>();
        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle(d => d.Id == delivery.Id);
    }

    [Fact]
    public async Task CancelWebhookDelivery_ShouldSetStatusToCanceled()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Cancel Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var hook = new Webhook
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Name = "Hook 1",
            Url = "https://example.com/hook",
            Events = ["flag.created"],
            SecretKey = "whsec_123"
        };
        db.Webhooks.Add(hook);

        var delivery = new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            WebhookId = hook.Id,
            EventName = "flag.created",
            Payload = "{}",
            Status = WebhookDeliveryStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.WebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.PostAsync($"/api/v1/projects/{project.Id}/webhooks/{hook.Id}/deliveries/{delivery.Id}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deliveryInDb = await db.WebhookDeliveries.AsNoTracking().FirstOrDefaultAsync(d => d.Id == delivery.Id);
        deliveryInDb.Should().NotBeNull();
        deliveryInDb!.Status.Should().Be(WebhookDeliveryStatus.Canceled);
    }

    [Fact]
    public async Task RetryWebhookDelivery_ShouldResetStatusToPending()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Retry Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var hook = new Webhook
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Name = "Hook 1",
            Url = "https://example.com/hook",
            Events = ["flag.created"],
            SecretKey = "whsec_123"
        };
        db.Webhooks.Add(hook);

        var delivery = new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            WebhookId = hook.Id,
            EventName = "flag.created",
            Payload = "{}",
            Status = WebhookDeliveryStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            AttemptCount = 3
        };
        db.WebhookDeliveries.Add(delivery);
        await db.SaveChangesAsync();

        // Act
        var response = await _client.PostAsync($"/api/v1/projects/{project.Id}/webhooks/{hook.Id}/deliveries/{delivery.Id}/retry", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deliveryInDb = await db.WebhookDeliveries.AsNoTracking().FirstOrDefaultAsync(d => d.Id == delivery.Id);
        deliveryInDb.Should().NotBeNull();
        deliveryInDb!.Status.Should().Be(WebhookDeliveryStatus.Pending);
        deliveryInDb.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateWebhookStatus_ShouldChangeStatus()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project { Name = "Status Project" };
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember { Project = project, UserId = Guid.Parse(TestAuthHandler.TestUserId), Role = ProjectRole.Owner });

        var hook = new Webhook
        {
            Id = Guid.CreateVersion7(),
            ProjectId = project.Id,
            Name = "Hook 1",
            Url = "https://example.com/hook",
            Events = ["flag.created"],
            SecretKey = "whsec_123",
            Status = WebhookStatus.Active
        };
        db.Webhooks.Add(hook);
        await db.SaveChangesAsync();

        var request = new UpdateWebhookStatusRequest
        {
            Status = WebhookStatus.Paused
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/projects/{project.Id}/webhooks/{hook.Id}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var hookInDb = await db.Webhooks.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hook.Id);
        hookInDb.Should().NotBeNull();
        hookInDb!.Status.Should().Be(WebhookStatus.Paused);
    }
}