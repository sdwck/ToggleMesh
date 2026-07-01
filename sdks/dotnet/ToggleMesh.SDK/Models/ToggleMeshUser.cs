using ToggleMesh.Common.Contexts;

namespace ToggleMesh.SDK.Models;

public readonly struct ToggleMeshUser<TContext> where TContext : IContextAccessor
{
    public readonly string Identity;
    public readonly TContext Context;

    public ToggleMeshUser(string identity, TContext context)
    {
        Identity = identity ?? string.Empty;
        Context = context;
    }
}
