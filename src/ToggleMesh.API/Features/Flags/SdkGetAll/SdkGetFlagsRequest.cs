using FastEndpoints;
using ToggleMesh.API.Features.Projects.Domain;
using ToggleMesh.API.Infrastructure;

namespace ToggleMesh.API.Features.Flags.SdkGetAll;

public class SdkGetFlagsRequest : ISdkRequest
{
    [HideFromDocs]
    public Guid EnvId { get; set; }
    [HideFromDocs]
    public KeyType KeyType { get; set; }
}
