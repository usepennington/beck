using System.Globalization;
using System.Text.RegularExpressions;

namespace Beck.Model;

/// <summary>
/// JavaScript semantics the model port depends on: JS <c>number</c> is a double,
/// <c>Math.round</c> rounds half toward +∞ (not banker's), and
/// <c>String(number)</c> prints integers without a decimal point — none of which
/// match .NET's defaults.
/// </summary>
internal static class Js
{
    // Decimal/float grammar accepted by JS Number() (the range diagrams use).
    private static readonly Regex DecimalRe =
        new(@"^[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$", RegexOptions.Compiled);

    /// <summary>JS <c>Math.round</c>: half rounds toward +∞ (not banker's, not away-from-zero).</summary>
    public static double Round(double x) => Math.Floor(x + 0.5);

    /// <summary>JS <c>String(number)</c>: integral values print without a decimal point.</summary>
    public static string Str(double n)
    {
        if (n == 0) return "0"; // normalizes -0 → "0"
        if (n == Math.Floor(n) && Math.Abs(n) < 1e21)
            return n.ToString("0", CultureInfo.InvariantCulture);
        // Default (no format) is shortest round-trippable in .NET Core 3+; matches
        // ECMAScript for the fixed-notation range diagrams use.
        return n.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>JS <c>String(value)</c> for the values the coercion layer sees.</summary>
    public static string Str(object? v) => v switch
    {
        null => "null",
        string s => s,
        bool b => b ? "true" : "false",
        double d => Str(d),
        IReadOnlyList<object?> list => string.Join(",", list.Select(e => e is null ? "" : Str(e))),
        IReadOnlyDictionary<string, object?> => "[object Object]",
        _ => v.ToString() ?? "",
    };

    /// <summary>JS <c>Number(string)</c> for the decimal range diagrams use; false when NaN.</summary>
    public static bool TryNumber(string raw, out double value)
    {
        value = 0;
        string s = raw.Trim();
        return s.Length > 0 && DecimalRe.IsMatch(s)
            && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
