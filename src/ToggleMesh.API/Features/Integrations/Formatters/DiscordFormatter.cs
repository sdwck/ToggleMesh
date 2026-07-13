using ToggleMesh.API.Features.Integrations.Domain;

namespace ToggleMesh.API.Features.Integrations.Formatters;

public class DiscordFormatter : IIntegrationFormatter
{
    public object FormatMessage(IntegrationEvent evt)
    {
        var text = GetEventText(evt);
        var color = GetEventColorDecimal(evt);

        var embed = new Dictionary<string, object>
        {
            ["title"] = text,
            ["color"] = color,
            ["timestamp"] = evt.Timestamp.UtcDateTime.ToString("O")
        };

        var fields = new List<object>
        {
            new
            {
                name = "Project",
                value = evt.ProjectName,
                inline = true
            }
        };

        if (!string.IsNullOrEmpty(evt.EnvironmentName))
        {
            fields.Add(new
            {
                name = "Environment",
                value = evt.EnvironmentName,
                inline = true
            });
        }

        if (!string.IsNullOrEmpty(evt.FlagKey))
        {
            fields.Add(new
            {
                name = "Flag",
                value = $"`{evt.FlagKey}`",
                inline = true
            });
        }

        if (!string.IsNullOrEmpty(evt.AdminBaseUrl) && !string.IsNullOrEmpty(evt.FlagKey))
            embed["url"] = $"{evt.AdminBaseUrl}/projects/{evt.ProjectName}/flags/{evt.FlagKey}";

        if (!string.IsNullOrEmpty(evt.ContextMessage))
            embed["description"] = $"**Context:** {evt.ContextMessage}";

        embed["fields"] = fields;

        return new
        {
            embeds = new[] { embed }
        };
    }

    private string GetEventText(IntegrationEvent evt)
    {
        return evt.EventName switch
        {
            "flag.created" => $"Feature flag created by {evt.ActorEmail ?? "System"}",
            "flag.updated" => $"Feature flag updated by {evt.ActorEmail ?? "System"}",
            "flag.deleted" => $"Feature flag deleted by {evt.ActorEmail ?? "System"}",
            "experiment.started" => $"Experiment started by {evt.ActorEmail ?? "System"}",
            "experiment.stopped" => $"Experiment stopped by {evt.ActorEmail ?? "System"}",
            "experiment.srm_detected" => "⚠️ Sample Ratio Mismatch (SRM) detected!",
            "integration.test" => "Test message from ToggleMesh integrations",
            _ => $"ToggleMesh event: {evt.EventName}"
        };
    }

    private int GetEventColorDecimal(IntegrationEvent evt)
    {
        return evt.EventName switch
        {
            "flag.created" => 3581519,
            "flag.updated" => 4431840,
            "flag.deleted" => 14687834,
            "experiment.started" => 3581519,
            "experiment.stopped" => 16753920,
            "experiment.srm_detected" => 14687834,
            "integration.test" => 4431840,
            _ => 8421504
        };
    }
}
