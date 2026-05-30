using Microsoft.Extensions.Hosting;

namespace ToggleMesh.SDK.Clients;

public interface IToggleMeshClient
{
    bool IsEnabled(string flagKey);
}