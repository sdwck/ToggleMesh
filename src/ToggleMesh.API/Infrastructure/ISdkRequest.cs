using ToggleMesh.API.Features.Projects;

namespace ToggleMesh.API.Infrastructure;

public interface ISdkRequest
{
    string ApiKey { get; set; }
    Guid EnvId { get; set; }
    KeyType KeyType { get; set; }
}