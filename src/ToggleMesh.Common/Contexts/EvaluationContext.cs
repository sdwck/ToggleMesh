// ReSharper disable ForCanBeConvertedToForeach
namespace ToggleMesh.Common.Contexts;

public readonly ref struct EvaluationContext<TAccessor> where TAccessor : IContextAccessor
{
    private readonly TAccessor _contextAccessor;
    private readonly IToggleMeshContextProvider[] _contextProviders;
    private readonly string[] _identityKeys;

    public EvaluationContext(
        TAccessor contextAccessor, 
        IToggleMeshContextProvider[] contextProviders, 
        string[] identityKeys)
    {
        _contextAccessor = contextAccessor;
        _contextProviders = contextProviders;
        _identityKeys = identityKeys;
    }

    public bool TryGetValue(string key, out string? value)
    {
        if (_contextAccessor.TryGetValue(key, out value))
            return true;

        if (_contextProviders.Length > 0)
            for (var i = 0; i < _contextProviders.Length; i++)
                if (_contextProviders[i].TryGetValue(key, out value))
                    return true;

        value = null;
        return false;
    }

    public string GetIdentity(string explicitIdentity)
    {
        if (!string.IsNullOrWhiteSpace(explicitIdentity)) 
            return explicitIdentity;

        for (var i = 0; i < _identityKeys.Length; i++)
            if (TryGetValue(_identityKeys[i], out var value) && !string.IsNullOrWhiteSpace(value))
                return value;

        return string.Empty;
    }
}