using System.Globalization;
using System.Reflection;
using System.Text;

namespace Beck.Docs.Components.Reference;

/// <summary>
/// Formats a reflected method or constructor as the plain C# signature text the API page
/// shows (it is then syntax-highlighted like any other code sample):
/// <c>DiagramBuilder Node(string id, Action&lt;NodeBuilder&gt;? configure = null)</c>.
/// </summary>
internal static class SignatureFormatter
{
    private static readonly NullabilityInfoContext Nullability = new();

    public static string Format(MethodBase method)
    {
        var sb = new StringBuilder();
        if (method is ConstructorInfo)
        {
            sb.Append("new ").Append(FriendlyName(method.DeclaringType!));
        }
        else
        {
            var m = (MethodInfo)method;
            if (m.IsStatic) sb.Append("static ");
            sb.Append(FriendlyName(m.ReturnType)).Append(' ').Append(m.Name);
        }

        sb.Append('(');
        var parameters = method.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            AppendParameter(sb, parameters[i]);
        }
        sb.Append(')');
        return sb.ToString();
    }

    private static void AppendParameter(StringBuilder sb, ParameterInfo p)
    {
        if (p.GetCustomAttribute<ParamArrayAttribute>() != null) sb.Append("params ");

        var name = FriendlyName(p.ParameterType);
        // Nullable value types already render as T?; restore the ? on annotated reference types.
        if (!p.ParameterType.IsValueType &&
            Nullability.Create(p).WriteState == NullabilityState.Nullable)
        {
            name += "?";
        }
        sb.Append(name).Append(' ').Append(p.Name);

        if (p.HasDefaultValue)
        {
            sb.Append(" = ").Append(p.DefaultValue switch
            {
                null => "null",
                bool b => b ? "true" : "false",
                string s => $"\"{s}\"",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                var v => v.ToString(),
            });
        }
    }

    /// <summary>
    /// C#-style type name: <c>Action&lt;NodeBuilder&gt;</c>, <c>int?</c>, <c>string[]</c>.
    /// A trimmed copy of the engine's internal <c>ReflectionModel.FriendlyName</c>
    /// (dotnet/Beck/Authoring/ClassBuilder.cs) — kept local rather than exposing the
    /// engine's internals to the docs site.
    /// </summary>
    public static string FriendlyName(Type t)
    {
        if (Primitives.TryGetValue(t, out var primitive)) return primitive;
        if (Nullable.GetUnderlyingType(t) is { } underlying) return FriendlyName(underlying) + "?";
        if (t.IsArray) return FriendlyName(t.GetElementType()!) + "[]";
        if (t.IsGenericType)
        {
            var name = t.Name[..t.Name.IndexOf('`')];
            return $"{name}<{string.Join(", ", t.GetGenericArguments().Select(FriendlyName))}>";
        }
        return t.Name;
    }

    private static readonly Dictionary<Type, string> Primitives = new()
    {
        [typeof(void)] = "void",
        [typeof(bool)] = "bool",
        [typeof(double)] = "double",
        [typeof(int)] = "int",
        [typeof(object)] = "object",
        [typeof(string)] = "string",
    };
}
