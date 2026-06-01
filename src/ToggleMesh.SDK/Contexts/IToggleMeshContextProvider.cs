namespace ToggleMesh.SDK.Contexts;

public interface IToggleMeshContextProvider
{
    Dictionary<string, string> GetContext();
}