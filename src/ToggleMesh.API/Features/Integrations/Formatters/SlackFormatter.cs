using ToggleMesh.API.Features.Integrations.Domain;

namespace ToggleMesh.API.Features.Integrations.Formatters;

public class SlackFormatter : IIntegrationFormatter
{
    public object FormatMessage(IntegrationEvent evt)
    {
        var text = GetEventText(evt);
        var color = GetEventColor(evt);

        var blocks = new List<object>
        {
            new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"*{text}*"
                }
            },
        };

        if (!string.IsNullOrEmpty(evt.ContextMessage))
        {
            blocks.Add(new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"*Context:* {evt.ContextMessage}"
                }
            });
        }

        blocks.Add(new
        {
            type = "context",
            elements = new List<object>
            {
                new
                {
                    type = "mrkdwn",
                    text = $"*Project:* {evt.ProjectName}"
                }
            }
        });

        if (!string.IsNullOrEmpty(evt.EnvironmentName))
        {
            dynamic contextBlock = blocks[1];
            contextBlock.elements.Add(new
            {
                type = "mrkdwn",
                text = $"*Environment:* {evt.EnvironmentName}"
            });
        }

        if (!string.IsNullOrEmpty(evt.FlagKey))
        {
            dynamic contextBlock = blocks[1];
            contextBlock.elements.Add(new
            {
                type = "mrkdwn",
                text = $"*Flag:* `{evt.FlagKey}`"
            });
        }

        if (!string.IsNullOrEmpty(evt.AdminBaseUrl) && !string.IsNullOrEmpty(evt.FlagKey))
        {
            blocks.Add(new
            {
                type = "actions",
                elements = new List<object>
                {
                    new
                    {
                        type = "button",
                        text = new
                        {
                            type = "plain_text",
                            text = "View in ToggleMesh"
                        },
                        url = $"{evt.AdminBaseUrl}/projects/{evt.ProjectName}/flags/{evt.FlagKey}"
                    }
                }
            });
        }

        return new
        {
            attachments = new[]
            {
                new
                {
                    color, 
                    blocks
                }
            }
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

    private string GetEventColor(IntegrationEvent evt)
    {
        return evt.EventName switch
        {
            "flag.created" => "#36a64f",
            "flag.updated" => "#439FE0",
            "flag.deleted" => "#E01E5A",
            "experiment.started" => "#36a64f",
            "experiment.stopped" => "#FFA500",
            "experiment.srm_detected" => "#E01E5A",
            "integration.test" => "#439FE0",
            _ => "#808080"
        };
    }
}
