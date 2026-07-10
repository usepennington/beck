using System.Text.RegularExpressions;

namespace Beck.Authoring;

/// <summary>
/// A tiny, dependency-free YAML scalar/flow emitter — just enough to write the
/// fixed Beck schema (maps, sequences, and quoted scalars). Not a general
/// serializer.
/// </summary>
internal static partial class YamlWriter
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "null", "yes", "no", "on", "off", "~",
    };
    
    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex NumberRegex();

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
        // Quote anything that starts with a digit. The decimal NumberRegex above misses the
        // other forms a YAML 1.1 reader (js-yaml) resolves as a number — hex (0x1F), octal
        // (0o17), binary (0b101), and exponents (1e3) — which would otherwise round-trip an
        // id/title like "0xFF" back as the number 255. Digit-leading identifiers are rare and
        // quoting them is always safe, so this is the simplest robust guard.
        if (char.IsDigit(v[0])) return false;
        if (!(char.IsLetterOrDigit(v[0]) || v[0] == '_')) return false;
        if (v[^1] == ' ') return false;
        const string extra = " _.()/+-";
        foreach (var ch in v)
            if (!(char.IsLetterOrDigit(ch) || extra.IndexOf(ch) >= 0))
                return false;
        return true;
    }

    // Double-quote and escape. The \r/\n/\t cases stay readable; every other C0 control
    // char (< 0x20) becomes a \xNN escape — emitting it raw makes js-yaml throw "expected
    // valid JSON character" and fails the whole diagram block.
    private static string Quote(string v)
    {
        var sb = new System.Text.StringBuilder(v.Length + 2);
        sb.Append('"');
        foreach (var ch in v)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20)
                        sb.Append("\\x").Append(((int)ch).ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                    else
                        sb.Append(ch);
                    break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}
