using FastEndpoints;

namespace ToggleMesh.API.Features.Flags.SdkGetAll;

public class SdkGetFlagsRequest
{
    [FromHeader("x-api-key")] 
    public string ApiKey { get; set; } = string.Empty;
}