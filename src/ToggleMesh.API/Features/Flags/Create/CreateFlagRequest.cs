using FastEndpoints;

namespace ToggleMesh.API.Features.Flags.Create;

public class CreateFlagRequest
{
    public Guid EnvironmentId { get; set; }
    public string Key { get; set; } = string.Empty;
}