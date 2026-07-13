using System.Text.Json;
using ToggleMesh.API.Features.Integrations.Domain;
using ToggleMesh.API.Features.Integrations.Formatters;

namespace ToggleMesh.IntegrationTests.Integrations;

public class IntegrationFormattersTests
{
    private readonly IntegrationEvent _testEvent = new(
        EventName: "flag.updated",
        ProjectName: "Test Project",
        EnvironmentName: "Production",
        FlagKey: "new-homepage",
        ActorEmail: "user@example.com",
        Timestamp: DateTimeOffset.UtcNow,
        AdminBaseUrl: "http://localhost:3000"
    );

    [Fact]
    public void SlackFormatter_ReturnsValidSlackPayload()
    {
        var formatter = new SlackFormatter();
        var result = formatter.FormatMessage(_testEvent);

        var json = JsonSerializer.Serialize(result);

        Assert.Contains("Feature flag updated by user@example.com", json);
        Assert.Contains("Test Project", json);
        Assert.Contains("Production", json);
        Assert.Contains("new-homepage", json);
        Assert.Contains("http://localhost:3000/projects/Test Project/flags/new-homepage", json);
        Assert.Contains("#439FE0", json);
    }

    [Fact]
    public void DiscordFormatter_ReturnsValidDiscordPayload()
    {
        var formatter = new DiscordFormatter();
        var result = formatter.FormatMessage(_testEvent);

        var json = JsonSerializer.Serialize(result);

        Assert.Contains("Feature flag updated by user@example.com", json);
        Assert.Contains("Test Project", json);
        Assert.Contains("Production", json);
        Assert.Contains("new-homepage", json);
        Assert.Contains("http://localhost:3000/projects/Test Project/flags/new-homepage", json);
        Assert.Contains("4431840", json);
    }

    [Fact]
    public void TeamsFormatter_ReturnsValidTeamsPayload()
    {
        var formatter = new TeamsFormatter();
        var result = formatter.FormatMessage(_testEvent);

        var json = JsonSerializer.Serialize(result);

        Assert.Contains("Feature flag updated by user@example.com", json);
        Assert.Contains("Test Project", json);
        Assert.Contains("Production", json);
        Assert.Contains("new-homepage", json);
        Assert.Contains("http://localhost:3000/projects/Test Project/flags/new-homepage", json);
        Assert.Contains("439FE0", json);
    }
}
