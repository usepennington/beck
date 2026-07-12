using System.Globalization;
using Beck.Model;

namespace Beck.Svg;

/// <summary>
/// Derives a chart's series colours from <c>--beck-primary</c> — the heart of the chart design
/// (<c>Charts.dc.html</c>). Each algorithm is a pure function of the token, emitted as a
/// <c>color-mix</c> / relative-colour (<c>oklch(from …)</c>) expression, never a resolved literal, so
/// the whole set re-tints with the host palette and flips light↔dark on the same switch as every
/// other Beck colour. A series that pins its own <c>color:</c> overrides its slot.
/// </summary>
internal static class ChartColors
{
    /// <summary>The <paramref name="n"/> series colours for <paramref name="palette"/>, in order.</summary>
    public static IReadOnlyList<string> Palette(ChartPalette palette, int n) => palette switch
    {
        ChartPalette.Monochromatic => Monochromatic(n),
        ChartPalette.Complementary => Complementary(n),
        ChartPalette.Sequential => Sequential(n),
        _ => Analogous(n),
    };

    /// <summary>Small hue steps either side of the primary — distinct yet harmonious; the categorical default.</summary>
    private static List<string> Analogous(int n)
    {
        var list = new List<string>(n);
        for (var i = 0; i < n; i++)
        {
            var d = RoundInt((i - (n - 1) / 2.0) * 26);
            list.Add($"oklch(from var(--beck-primary) l c {Hue(d)})");
        }

        return list;
    }

    /// <summary>Tints &amp; shades of the primary, mixed toward the surface — calm, single-hue.</summary>
    private static List<string> Monochromatic(int n)
    {
        var list = new List<string>(n);
        for (var i = 0; i < n; i++)
        {
            var t = n == 1 ? 0 : (double)i / (n - 1);
            list.Add($"color-mix(in oklab, var(--beck-primary), var(--beck-surface) {RoundInt(6 + t * 64)}%)");
        }

        return list;
    }

    /// <summary>Primary alternating with its opposite, lightening per pair — high contrast.</summary>
    private static List<string> Complementary(int n)
    {
        var list = new List<string>(n);
        for (var i = 0; i < n; i++)
        {
            int side = i % 2, grp = i / 2;
            var l = Math.Round(grp * 0.1, 2);
            list.Add($"oklch(from var(--beck-primary) {Lightness(l)} c {Hue(side * 180)})");
        }

        return list;
    }

    /// <summary>Primary fading toward neutral — reads as one continuous scale (density/heat).</summary>
    private static List<string> Sequential(int n)
    {
        var list = new List<string>(n);
        for (var i = 0; i < n; i++)
        {
            var t = n == 1 ? 0 : (double)i / (n - 1);
            list.Add($"color-mix(in oklab, var(--beck-primary), var(--beck-neutral) {RoundInt(4 + t * 72)}%)");
        }

        return list;
    }

    /// <summary>A relative-colour hue channel: <c>h</c>, or <c>calc(h ± d)</c> degrees off it.</summary>
    private static string Hue(int d) => d == 0 ? "h" : d > 0 ? $"calc(h + {d})" : $"calc(h - {-d})";

    /// <summary>A relative-colour lightness channel: <c>l</c>, or <c>calc(l + d)</c>.</summary>
    private static string Lightness(double d) =>
        d == 0 ? "l" : $"calc(l + {d.ToString("0.##", CultureInfo.InvariantCulture)})";

    /// <summary>JS <c>Math.round</c> (half toward +∞) — the reference impl rounds mix percentages this way.</summary>
    private static int RoundInt(double x) => (int)Math.Floor(x + 0.5);
}
