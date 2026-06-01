using System.Linq.Expressions;
using System.Reflection;

namespace ToggleMesh.SDK.Contexts;

public static class ContextMapper<T>
{
    private static readonly Func<T, IDictionary<string, string>> Mapper;

    static ContextMapper()
    {
        var type = typeof(T);

        if (typeof(IDictionary<string, string>).IsAssignableFrom(type))
        {
            Mapper = obj => (IDictionary<string, string>)obj!;
            return;
        }

        var input = Expression.Parameter(type, "obj");
        var dictType = typeof(Dictionary<string, string>);
        var dictAdd = dictType.GetMethod("Add", [typeof(string), typeof(string)]);
        var newDict = Expression.New(dictType);

        var variables = new List<ParameterExpression>();
        var expressions = new List<Expression>();
        var dictVar = Expression.Variable(dictType, "dict");
        variables.Add(dictVar);
        expressions.Add(Expression.Assign(dictVar, newDict));

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead) continue;

            var propAccess = Expression.Property(input, prop);
            var propValueAsObj = Expression.Convert(propAccess, typeof(object));
            var toStringMethod = typeof(Convert).GetMethod("ToString", [typeof(object)]);
            var stringValue = Expression.Call(toStringMethod!, propValueAsObj);

            var keyExpr = Expression.Constant(prop.Name);
            var addExpr = Expression.Call(dictVar, dictAdd!, keyExpr, stringValue);

            var nullCheck = Expression.NotEqual(propValueAsObj, Expression.Constant(null, typeof(object)));
            var conditionalAdd = Expression.IfThen(nullCheck, addExpr);

            expressions.Add(conditionalAdd);
        }

        var castDict = Expression.Convert(dictVar, typeof(IDictionary<string, string>));
        expressions.Add(castDict);

        var block = Expression.Block(variables, expressions);
        Mapper = Expression.Lambda<Func<T, IDictionary<string, string>>>(block, input).Compile();
    }

    public static IDictionary<string, string> ToDictionary(T obj)
    {
        if (obj == null) return new Dictionary<string, string>();
        return Mapper(obj);
    }
}