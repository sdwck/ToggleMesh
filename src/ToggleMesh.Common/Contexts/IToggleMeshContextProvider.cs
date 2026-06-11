namespace ToggleMesh.Common.Contexts;

public interface IToggleMeshContextProvider
{
    bool TryGetValue(string key, out string? value);
}