using System.Globalization;
using System.Text;

namespace Beck.Rendering.Svg;

/// <summary>Small SVG formatting helpers: coordinate rounding and XML escaping.</summary>
internal static class SvgWriter
{
    /// <summary>Format a coordinate: 2-decimal, JS <c>Math.round</c>, invariant culture.</summary>
    public static string Num(double n) => Js.Str(Js.Round(n * 100) / 100);

    /// <summary>Format a letter-spacing em value as a CSS length (e.g. 0.04 → <c>0.04em</c>,
    /// -0.02 → <c>-0.02em</c>). Prints the em value directly (shortest round-trippable).</summary>
    public static string Ls(double em) => Js.Str(em) + "em";

    /// <summary>Format a style integer (mix percentage, font weight) invariantly, so a comma-decimal
    /// locale can never perturb the emitted CSS/SVG.</summary>
    public static string Int(int n) => n.ToString(CultureInfo.InvariantCulture);

    /// <summary>Escape text content (&amp;, &lt;, &gt;).</summary>
    public static string Text(string s)
    {
        if (s.IndexOfAny(new[] { '&', '<', '>' }) < 0) return s;
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>Escape an attribute value (adds quotes).</summary>
    public static string Attr(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
