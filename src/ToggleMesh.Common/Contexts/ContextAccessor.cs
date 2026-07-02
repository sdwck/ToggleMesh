using System.Linq.Expressions;
using System.Reflection;

namespace ToggleMesh.Common.Contexts;

public readonly struct ContextAccessor<T> : IContextAccessor
{
    private readonly T _instance;
    private static readonly Dictionary<string, Func<T, string?>> Getters;
    // ReSharper disable once StaticMemberInGenericType
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
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            var propertyAccess = Expression.Property(input, property);

            Expression stringValue;
            if (property.PropertyType == typeof(string))
                stringValue = propertyAccess;
            else
            {
                var nullableUnderlyingType = Nullable.GetUnderlyingType(property.PropertyType);
                if (nullableUnderlyingType != null)
                {
                    var hasValueProperty = property.PropertyType.GetProperty("HasValue");
                    var valueProperty = property.PropertyType.GetProperty("Value");
                    
                    var hasValue = Expression.Property(propertyAccess, hasValueProperty!);
                    var getValue = Expression.Property(propertyAccess, valueProperty!);
                    
                    var toStringMethod = nullableUnderlyingType.GetMethod("ToString", Type.EmptyTypes);
                    var callToString = Expression.Call(getValue, toStringMethod!);

                    stringValue = Expression.Condition(
                        hasValue,
                        callToString,
                        Expression.Constant(null, typeof(string)));
                }
                else if (property.PropertyType.IsValueType)
                {
                    var toStringMethod = property.PropertyType.GetMethod("ToString", Type.EmptyTypes);
                    stringValue = Expression.Call(propertyAccess, toStringMethod!);
                }
                else
                {
                    var toStringMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes);
                    var isNull = Expression.Equal(propertyAccess, Expression.Constant(null, property.PropertyType));
                    var callToString = Expression.Call(propertyAccess, toStringMethod!);
                    
                    stringValue = Expression.Condition(
                        isNull,
                        Expression.Constant(null, typeof(string)),
                        callToString);
                }
            }
            
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