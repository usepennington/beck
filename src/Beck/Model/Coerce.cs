namespace Beck.Model;

/// <summary>
/// Low-level coercion helpers shared by every diagram-type builder — a port of
/// <c>src/model/coerce.ts</c>. Each throws a <see cref="BeckYamlException"/> with a
/// friendly field path instead of letting a raw type error leak. Operates on the
/// untyped <c>object?</c> tree from <see cref="YamlLoader"/>.
/// </summary>
internal static class Coerce
{
    private static readonly Dictionary<string, object?> _emptyMap = new();
    private static readonly List<object?> _emptyList = [];

    public static IReadOnlyDictionary<string, object?> AsObject(object? v, string field)
    {
        if (v is null)
        {
            return _emptyMap;
        }

        if (v is IReadOnlyDictionary<string, object?> d)
        {
            return d;
        }

        throw new BeckYamlException($"`{field}` must be a mapping");
    }

    public static IReadOnlyList<object?> AsArray(object? v, string field)
    {
        if (v is null)
        {
            return _emptyList;
        }

        if (v is IReadOnlyList<object?> l)
        {
            return l;
        }

        throw new BeckYamlException($"`{field}` must be a list");
    }

    public static string AsString(object? v, string field)
    {
        if (v is string s)
        {
            return s;
        }

        if (v is double or bool)
        {
            return Js.Str(v);
        }

        throw new BeckYamlException($"`{field}` must be a string");
    }

    public static string? OptString(object? v)
    {
        if (v is null)
        {
            return null;
        }

        if (v is string s)
        {
            return s;
        }

        if (v is double or bool)
        {
            return Js.Str(v);
        }

        return null; // non-scalars silently dropped
    }

    public static string? OptColor(object? v)
    {
        var s = OptString(v);
        return s is null ? null : Colors.AccentToCss(s, AccentToken.Primary);
    }

    public static double? OptNumber(object? v, string field)
    {
        if (v is null)
        {
            return null;
        }

        if (v is double d)
        {
            return d;
        }

        if (v is string s && Js.TryNumber(s, out var n))
        {
            return n;
        }

        throw new BeckYamlException($"`{field}` must be a number");
    }

    public static bool OptBool(object? v, string field, bool dflt)
    {
        if (v is null)
        {
            return dflt;
        }

        if (v is bool b)
        {
            return b;
        }

        if (v is "true")
        {
            return true;
        }

        if (v is "false")
        {
            return false;
        }

        throw new BeckYamlException($"`{field}` must be true or false");
    }

    /// <summary>Tri-state boolean: null when unset (so a heuristic can decide later).</summary>
    public static bool? TriBool(object? v, string field)
    {
        if (v is null)
        {
            return null;
        }

        return OptBool(v, field, false);
    }

    public static TEnum OneOf<TEnum>(object? v, TokenMap<TEnum> map, string field, TEnum dflt)
        where TEnum : struct, Enum
    {
        if (v is null)
        {
            return dflt;
        }

        var s = Js.Str(v);
        if (map.TryParse(s, out var e))
        {
            return e;
        }

        throw new BeckYamlException(
            $"`{field}` must be one of: {string.Join(", ", map.Tokens)} (got \"{s}\")");
    }

    /// <summary>String variant for transient vocabularies not stored as an enum (e.g. relation kind).</summary>
    public static string OneOfString(object? v, IReadOnlyList<string> allowed, string field, string dflt)
    {
        if (v is null)
        {
            return dflt;
        }

        var s = Js.Str(v);
        if (allowed.Contains(s))
        {
            return s;
        }

        throw new BeckYamlException(
            $"`{field}` must be one of: {string.Join(", ", allowed)} (got \"{s}\")");
    }

    /// <summary>Coerce a list of scalars (class fields/methods and similar line lists).</summary>
    public static List<string> StringList(object? v, string field) =>
        AsArray(v, field).Select(s => AsString(s, field)).ToList();
}