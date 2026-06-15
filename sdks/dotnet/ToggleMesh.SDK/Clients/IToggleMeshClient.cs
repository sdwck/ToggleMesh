namespace ToggleMesh.SDK.Clients;

public interface IToggleMeshClient
{
    bool IsEnabled(string flagKey, bool defaultValue = false);
    bool IsEnabled(string flagKey, string identity, bool defaultValue = false);
    bool IsEnabled(string flagKey, IDictionary<string, string> context, bool defaultValue = false);
    bool IsEnabled(string flagKey, string identity, IDictionary<string, string> context, bool defaultValue = false);
    bool IsEnabled<TContext>(string flagKey, TContext contextObject, bool defaultValue = false);
    bool IsEnabled<TContext>(string flagKey, string identity, TContext contextObject, bool defaultValue = false);
}