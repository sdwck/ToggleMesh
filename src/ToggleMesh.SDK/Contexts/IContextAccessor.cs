namespace ToggleMesh.SDK.Contexts;

public interface IContextAccessor
{
    bool TryGetValue(string key, out string? value);
}