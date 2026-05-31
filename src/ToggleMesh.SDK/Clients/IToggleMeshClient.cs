namespace ToggleMesh.SDK.Clients;

public interface IToggleMeshClient
{
    bool IsEnabled(string flagKey, bool defaultValue = false);
}