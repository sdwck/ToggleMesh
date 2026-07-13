using ToggleMesh.API.Features.Integrations.Domain;

namespace ToggleMesh.API.Features.Integrations.Formatters;

public class TeamsFormatter : IIntegrationFormatter
{
    public object FormatMessage(IntegrationEvent evt)
    {
        var text = GetEventText(evt);

        var facts = new List<object>
        {
            new
            {
                title = "Project",
                value = evt.ProjectName
            }
        };

        if (!string.IsNullOrEmpty(evt.EnvironmentName))
        {
            facts.Add(new
            {
                title = "Environment",
                value = evt.EnvironmentName
            });
        }

        if (!string.IsNullOrEmpty(evt.FlagKey))
        {
            facts.Add(new
            {
                title = "Flag",
                value = evt.FlagKey
            });
        }

        var sections = new List<Dictionary<string, object>>
        {
            new()
            {
                ["activityTitle"] = text,
                ["facts"] = facts,
                ["markdown"] = true
            }
        };

        if (!string.IsNullOrEmpty(evt.ContextMessage))
        {
            sections.Add(new Dictionary<string, object>
            {
                ["text"] = $"**Context:** {evt.ContextMessage}",
                ["markdown"] = true
            });
        }

        var card = new Dictionary<string, object>
        {
            ["@type"] = "MessageCard",
            ["@context"] = "http://schema.org/extensions",
            ["themeColor"] = GetEventColorHex(evt),
            ["summary"] = text,
            ["sections"] = sections
        };

        if (!string.IsNullOrEmpty(evt.AdminBaseUrl) && !string.IsNullOrEmpty(evt.FlagKey))
        {
            card["potentialAction"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["@type"] = "OpenUri",
                    ["name"] = "View in ToggleMesh",
                    ["targets"] = new[]
                    {
                        new { os = "default", uri = $"{evt.AdminBaseUrl}/projects/{evt.ProjectName}/flags/{evt.FlagKey}" }
                    }
                }
            };
        }

        return card;
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

    private string GetEventColorHex(IntegrationEvent evt)
    {
        return evt.EventName switch
        {
            "flag.created" => "36A64F",
            "flag.updated" => "439FE0",
            "flag.deleted" => "E01E5A",
            "experiment.started" => "36A64F",
            "experiment.stopped" => "FFA500",
            "experiment.srm_detected" => "E01E5A",
            "integration.test" => "439FE0",
            _ => "808080"
        };
    }
}
