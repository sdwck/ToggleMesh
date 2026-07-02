using ToggleMesh.API.Features.Projects.Domain;

namespace ToggleMesh.API.Infrastructure;

public interface ISdkRequest
{
    Guid EnvId { get; set; }
    KeyType KeyType { get; set; }
}