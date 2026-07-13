using System.Text.Json.Serialization;

namespace ToggleMesh.API.Features.Integrations.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntegrationProvider
{
    Slack,
    Discord,
    MicrosoftTeams
}
