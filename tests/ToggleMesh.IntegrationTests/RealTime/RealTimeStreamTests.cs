using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ToggleMesh.API.Features.Projects.CreateProject;
using ToggleMesh.API.Features.Flags.Create;
using ToggleMesh.API.Infrastructure.Sse;
using ToggleMesh.IntegrationTests.Infrastructure;

namespace ToggleMesh.IntegrationTests.RealTime;

public class RealTimeStreamTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;
    private static readonly Guid TestOrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public RealTimeStreamTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MutatingProject_ShouldBroadcastInvalidateQueryKey()
    {
        var sseService = _factory.Services.GetRequiredService<ISseService>();
        var testUserId = Guid.Parse(TestAuthHandler.TestUserId);

        var receivedEvents = new List<(string EventName, string Data)>();
        using var cts = new CancellationTokenSource();

        sseService.Subscribe(testUserId, (eventName, data) =>
        {
            lock (receivedEvents)
            {
                receivedEvents.Add((eventName, data));
            }
            return Task.CompletedTask;
        }, cts.Token);

        // Act
        var request = new CreateProjectRequest { Name = "SSE Test Project", OrganizationId = TestOrgId };
        var response = await _client.PostAsJsonAsync("/api/v1/projects", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(200);
        cts.Cancel();

        lock (receivedEvents)
        {
            receivedEvents.Should().NotBeEmpty("An invalidate event should be broadcasted on project creation.");
            var projectEvent = receivedEvents.FirstOrDefault(e => e.EventName == "invalidate" && e.Data.Contains("projects"));
            projectEvent.Should().NotBeNull("Projects query key should be invalidated.");
        }
    }

    [Fact]
    public async Task MutatingFeatureFlag_ShouldBroadcastFlagInvalidateQueryKey()
    {
        // Arrange
        var sseService = _factory.Services.GetRequiredService<ISseService>();
        var testUserId = Guid.Parse(TestAuthHandler.TestUserId);

        var receivedEvents = new List<(string EventName, string Data)>();
        using var cts = new CancellationTokenSource();

        sseService.Subscribe(testUserId, (eventName, data) =>
        {
            lock (receivedEvents)
            {
                receivedEvents.Add((eventName, data));
            }
            return Task.CompletedTask;
        }, cts.Token);

        var projectRequest = new CreateProjectRequest { Name = "SSE Flag Project", OrganizationId = TestOrgId };
        var projectResponse = await _client.PostAsJsonAsync("/api/v1/projects", projectRequest);
        projectResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var project = await projectResponse.Content.ReadFromJsonAsync<CreateProjectResponse>();
        project.Should().NotBeNull();

        // Act
        var flagRequest = new CreateFlagRequest { Key = "sse_flag" };
        var flagResponse = await _client.PostAsJsonAsync($"/api/v1/projects/{project!.Id}/flags", flagRequest);
        flagResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert
        await Task.Delay(200);
        cts.Cancel();

        lock (receivedEvents)
        {
            receivedEvents.Should().NotBeEmpty();
            var flagEvent = receivedEvents.FirstOrDefault(e => e.EventName == "invalidate" && e.Data.Contains("flags") && e.Data.Contains(project.Id.ToString()));
            flagEvent.Should().NotBeNull("Flag query key containing project ID should be invalidated.");
        }
    }
}
