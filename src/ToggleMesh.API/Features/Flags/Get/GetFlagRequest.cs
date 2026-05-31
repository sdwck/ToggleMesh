using Microsoft.AspNetCore.Mvc;

namespace ToggleMesh.API.Features.Flags.Get;

public class GetFlagRequest
{
    [FromQuery]
    public Guid EnvironmentId { get; set; }
}