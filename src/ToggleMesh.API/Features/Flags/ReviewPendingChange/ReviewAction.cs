using System.Text.Json.Serialization;

namespace ToggleMesh.API.Features.Flags.ReviewPendingChange;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewAction
{
    Approve,
    Reject
}
