using FastEndpoints;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Client.SdkEvaluateFlags;

public class SdkEvaluateFlagsRequest : ISdkRequest {
    [HideFromDocs]
    public Guid EnvId { get; set; }
    [HideFromDocs]
    public KeyType KeyType { get; set; }
    public string Identity { get; set; } = string.Empty;
    public Dictionary<string, string> Context { get; set; } = [];
}