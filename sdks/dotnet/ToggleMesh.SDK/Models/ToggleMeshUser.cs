using ToggleMesh.Common.Contexts;

namespace ToggleMesh.SDK.Models;

public struct ToggleMeshUser<TContext> where TContext : IContextAccessor
{
    public string Identity;
    public TContext Context;

    public ToggleMeshUser(string? identity, TContext context)
    {
        Identity = identity ?? string.Empty;
        Context = context;
    }
}
