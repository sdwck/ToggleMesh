using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ToggleMesh.API.Features.Flags.Domain;
using ToggleMesh.API.Features.Flags.Toggle;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Features.Webhooks.Domain;
using ToggleMesh.API.Features.Webhooks.Workers;
using ToggleMesh.API.Infrastructure.Data;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.Webhooks;

[Collection("SharedEnv1")]
public class WebhookDeliverySagaTests : IAsyncLifetime
{
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private readonly TestWebApplicationFactory _factory;

    public WebhookDeliverySagaTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Mock 500 internal server error")
            });
        }
    }

    [Fact]
    public async Task FailingWebhook_Should_Retry_WithBackoff_And_Eventually_DisableWebhook()
    {
        // Arrange
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("WebhookClient")
                    .ConfigurePrimaryHttpMessageHandler(() => new FailingHttpMessageHandler());
            });
        });

        var client = factory.CreateClient();
        var timeProvider = (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();

        Guid projectId;
        Guid envId;
        var flagKey = "webhook_saga_test_flag";
        Guid webhookId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var project = new Project { Name = "Webhook Saga Proj" };
            db.Projects.Add(project);
            db.ProjectMembers.Add(new ProjectMember
            {
                Project = project,
                UserId = Guid.Parse(TestAuthHandler.TestUserId),
                Role = ProjectRole.Owner
            });

            var environment = new ProjectEnvironment { Name = "Production", Project = project };
            db.Environments.Add(environment);

            var webhook = new Webhook
            {
                Project = project,
                Name = "Saga Webhook",
                Url = "https://example.com/webhook",
                SecretKey = "super-secret",
                Status = WebhookStatus.Active,
                EnvironmentIds = [],
                Events = ["flag.updated"]
            };
            db.Webhooks.Add(webhook);

            var flag = new FeatureFlag { Project = project, Key = flagKey };
            db.FeatureFlags.Add(flag);

            var state = new FlagEnvironmentState
            {
                Environment = environment,
                FeatureFlag = flag,
                IsEnabled = false
            };
            db.FlagEnvironmentStates.Add(state);

            await db.SaveChangesAsync();
            
            var drainChannel = factory.Services.GetRequiredService<Channel<WebhookEvent>>();
            while (drainChannel.Reader.TryRead(out _)) { }

            projectId = project.Id;
            envId = environment.Id;
            webhookId = webhook.Id;
        }

        var toggleUrl = $"/api/v1/projects/{projectId}/environments/{envId}/flags/{flagKey}/toggle";
        var toggleResponse = await client.PostAsJsonAsync(toggleUrl, new ToggleFlagRequest { IsEnabled = true });
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channel = factory.Services.GetRequiredService<Channel<WebhookEvent>>();
        var evt = await channel.Reader.ReadAsync();
        evt.EventName.Should().Be("flag.updated");

        var dispatcher = new WebhookDispatcherService(
            channel,
            factory.Services,
            factory.Services.GetRequiredService<ILogger<WebhookDispatcherService>>(),
            timeProvider);

        var dispatchMethod = typeof(WebhookDispatcherService).GetMethod("QueueDeliveriesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dispatchMethod.Should().NotBeNull();
        await (Task)dispatchMethod.Invoke(dispatcher, [evt, CancellationToken.None])!;

        Guid deliveryId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var delivery = await db.WebhookDeliveries.SingleAsync(d => d.WebhookId == webhookId);
            delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);
            delivery.AttemptCount.Should().Be(0);
            deliveryId = delivery.Id;
        }
        
        var worker = factory.Services.GetServices<IHostedService>().OfType<WebhookDeliveryWorker>().Single();
        var processMethod = typeof(WebhookDeliveryWorker).GetMethod("ProcessDeliveriesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        processMethod.Should().NotBeNull();
        
        await (Task)processMethod.Invoke(worker, [CancellationToken.None])!;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var delivery = await db.WebhookDeliveries.Include(d => d.Webhook).SingleAsync(d => d.Id == deliveryId);
            delivery.AttemptCount.Should().Be(1);
            delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);
            delivery.Webhook.ConsecutiveFailures.Should().Be(1);
            var expectedNext = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(1);
            delivery.NextAttemptAt.Should().BeCloseTo(expectedNext, TimeSpan.FromSeconds(5));
        }
        
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await (Task)processMethod.Invoke(worker, [CancellationToken.None])!;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var delivery = await db.WebhookDeliveries.Include(d => d.Webhook).SingleAsync(d => d.Id == deliveryId);
            delivery.AttemptCount.Should().Be(2);
            delivery.Webhook.ConsecutiveFailures.Should().Be(2);

            var expectedNext = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5);
            delivery.NextAttemptAt.Should().BeCloseTo(expectedNext, TimeSpan.FromSeconds(5));
        }
        
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var w = await db.Webhooks.SingleAsync(x => x.Id == webhookId);
            w.ConsecutiveFailures = 9;
            await db.SaveChangesAsync();
        }
        
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await (Task)processMethod.Invoke(worker, [CancellationToken.None])!;
        
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var w = await db.Webhooks.SingleAsync(x => x.Id == webhookId);
            w.ConsecutiveFailures.Should().Be(10);
            w.Status.Should().Be(WebhookStatus.DisabledBySystem, "Webhook should be automatically disabled after 10 consecutive failures");
        }
    }
}
