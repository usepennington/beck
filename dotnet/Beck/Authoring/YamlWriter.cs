using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Beck;

/// <summary>
/// A tiny, dependency-free YAML scalar/flow emitter — just enough to write the
/// fixed Beck schema (maps, sequences, and quoted scalars). Not a general
/// serializer.
/// </summary>
internal static partial class YamlWriter
{
    private static readonly HashSet<string> Reserved = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "null", "yes", "no", "on", "off", "~",
    };

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex NumberRegex();
#else
    private static readonly Regex _number = new(@"^-?\d+(\.\d+)?$", RegexOptions.Compiled);
    private static Regex NumberRegex() => _number;
#endif

    /// <summary>Emit a scalar, quoting (and escaping) only when YAML requires it.</summary>
    public static string Scalar(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return IsSafePlain(value!) ? value! : Quote(value!);
    }

    public static string FlowMap(IEnumerable<(string Key, string Value)> pairs) =>
        "{ " + string.Join(", ", pairs.Select(p => $"{p.Key}: {p.Value}")) + " }";

    public static string FlowSeq(IEnumerable<string> items) =>
        "[" + string.Join(", ", items) + "]";

    private static bool IsSafePlain(string v)
    {
        if (Reserved.Contains(v)) return false;
        if (NumberRegex().IsMatch(v)) return false;
        if (!(char.IsLetterOrDigit(v[0]) || v[0] == '_')) return false;
        if (v[v.Length - 1] == ' ') return false;
        const string extra = " _.()/+-";
        foreach (var ch in v)
            if (!(char.IsLetterOrDigit(ch) || extra.IndexOf(ch) >= 0))
                return false;
        return true;
    }

    private static string Quote(string v) =>
        "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
}
