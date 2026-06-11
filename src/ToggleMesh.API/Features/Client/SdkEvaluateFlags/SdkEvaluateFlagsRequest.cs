using FastEndpoints;
using ToggleMesh.API.Features.Projects;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Client.SdkEvaluateFlags;

public class SdkEvaluateFlagsRequest : ISdkRequest {
    [FromHeader("x-api-key")]
    public string ApiKey { get; set; } = string.Empty;
    [HideFromDocs]
    public Guid EnvId { get; set; }
    [HideFromDocs]
    public KeyType KeyType { get; set; }
    public string Identity { get; set; } = string.Empty;
    public Dictionary<string, string> Context { get; set; } = [];
}