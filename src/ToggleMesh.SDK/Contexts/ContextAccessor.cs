using System.Linq.Expressions;
using System.Reflection;

namespace ToggleMesh.SDK.Contexts;

public readonly struct ContextAccessor<T> : IContextAccessor
{
    private readonly T _instance;
    private static readonly Dictionary<string, Func<T, string?>> Getters;
    private static readonly bool IsDictionary;

    static ContextAccessor()
    {
        Getters = new Dictionary<string, Func<T, string?>>(StringComparer.OrdinalIgnoreCase);
        var type = typeof(T);

        if (typeof(IDictionary<string, string>).IsAssignableFrom(type))
        {
            IsDictionary = true;
            return;
        }

        var input = Expression.Parameter(type, "obj");
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
                continue;

            var propertyAccess = Expression.Property(input, property);
            var propertyValueAsObject = Expression.Convert(propertyAccess, typeof(object));
            var toStringMethod = typeof(Convert).GetMethod("ToString", [typeof(object)]);
            var stringValue = Expression.Call(toStringMethod!, propertyValueAsObject);

            var lambda = Expression.Lambda<Func<T, string?>>(stringValue, input).Compile();
            Getters[property.Name] = lambda;
        }
    }

    public ContextAccessor(T instance)
    {
        _instance = instance;
    }

    public bool TryGetValue(string key, out string? value)
    {
        if (_instance == null)
        {
            value = null;
            return false;
        }

        if (IsDictionary && _instance is IDictionary<string, string> dict)
            return dict.TryGetValue(key, out value);

        if (Getters.TryGetValue(key, out var getter))
        {
            value = getter(_instance);
            return value != null;
        }

        value = null;
        return false;
    }
}