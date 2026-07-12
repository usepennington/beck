using System.Globalization;
using System.Text.RegularExpressions;

namespace Beck.Tests.CleanLines;

/// <summary>
/// The chart chaos monkey's scorer. Charts have no routing to score for cleanliness (their own
/// painter draws straight to SVG), so the gate is <em>graceful degradation</em>: hostile data must
/// still render to a finite, on-canvas SVG rather than throw, emit <c>NaN</c>/<c>∞</c> coordinates,
/// or spill off the canvas. Every violation here is hard — a chart may look poor under insane numbers,
/// but it must never produce invalid output.
/// </summary>
internal static class ChartQuality
{
    internal readonly record struct Violation(string Kind, string Detail);

    public static IReadOnlyList<Violation> Analyze(string yaml)
    {
        var v = new List<Violation>();
        string svg;
        try
        {
            // The generator emits structurally valid charts, so a *successful* render is the contract;
            // any throw (a raw crash, or even a BeckYamlException on data we meant to be valid) fails.
            svg = BeckSvg.Render(yaml);
        }
        catch (Exception e)
        {
            v.Add(new("threw", $"{e.GetType().Name}: {Trunc(e.Message)}"));
            return v;
        }

        if (!svg.StartsWith("<svg", StringComparison.Ordinal))
        {
            v.Add(new("no-svg", "output does not start with <svg"));
            return v;
        }

        // Non-finite coordinates surface as these tokens once a number is stringified.
        foreach (var tok in new[] { "NaN", "Infinity", "∞" })
        {
            if (svg.Contains(tok, StringComparison.Ordinal))
            {
                v.Add(new("non-finite", $"output contains \"{tok}\""));
            }
        }

        // The canvas must be "0 0 W H" with finite, positive extents.
        var vb = Regex.Match(svg, @"viewBox=""0 0 (-?[0-9.]+) (-?[0-9.]+)""");
        if (!vb.Success)
        {
            v.Add(new("viewbox", "viewBox is not \"0 0 W H\""));
        }
        else
        {
            var w = double.Parse(vb.Groups[1].Value, CultureInfo.InvariantCulture);
            var h = double.Parse(vb.Groups[2].Value, CultureInfo.InvariantCulture);
            if (!(w > 0 && h > 0 && double.IsFinite(w) && double.IsFinite(h)))
            {
                v.Add(new("canvas", FormattableString.Invariant($"non-positive canvas {w}x{h}")));
            }
        }

        // No off-canvas (negative) drawing coordinate — the signature of a render bug. Colour
        // expressions (`calc(h - 52)`) and `letter-spacing="-0.02em"` live in style attributes, not the
        // geometry attributes / path data scanned here.
        foreach (Match m in Regex.Matches(svg, @"\b(?:x|y|cx|cy|x1|y1|x2|y2|width|height|rx|r)=""(-?[0-9.]+)"""))
        {
            if (m.Groups[1].Value.StartsWith('-'))
            {
                v.Add(new("off-canvas", $"negative geometry {m.Value}"));
                break;
            }
        }

        foreach (Match m in Regex.Matches(svg, @"(?:\bd|points)=""([^""]*)"""))
        {
            if (Regex.IsMatch(m.Groups[1].Value, "-[0-9]"))
            {
                v.Add(new("off-canvas", "negative path/points coordinate"));
                break;
            }
        }

        return v;
    }

    private static string Trunc(string s) => s.Length > 120 ? s[..120] : s;
}
