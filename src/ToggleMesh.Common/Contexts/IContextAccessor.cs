namespace ToggleMesh.Common.Contexts;

public interface IContextAccessor
{
    bool TryGetValue(string key, out string? value);
}