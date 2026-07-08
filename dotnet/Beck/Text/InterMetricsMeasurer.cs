namespace Beck.Rendering.Text;

/// <summary>
/// The default, zero-dependency text measurer. Backed by an embedded metrics table
/// (<see cref="InterMetricsData"/>) generated offline from Inter (sans) and IBM Plex Mono:
/// per-glyph advance-per-em per weight, plus per-em ascent/descent. Used whenever the caller
/// does not supply a font-file-backed exact measurer.
/// </summary>
/// <remarks>
/// This measures against Inter/Plex regardless of the font the SVG actually asks for, so card
/// sizing is a close <em>approximation</em> — the per-text <c>textLength</c> guard absorbs the
/// small mismatch. For pixel-accurate sizing over your site's real fonts, pass a
/// <c>Beck.Skia.SkiaTextMeasurer</c> (see the install guide). Advances are a plain
/// per-glyph sum, so kerning and ligatures are not modelled — negligible for the short Latin
/// labels diagrams use.
/// </remarks>
public sealed class InterMetricsMeasurer : ITextMeasurer
{
    /// <summary>The shared, immutable default measurer.</summary>
    public static InterMetricsMeasurer Instance { get; } = new();

    private InterMetricsMeasurer() { }

    // Codepoint → advance-per-em per sans weight, for glyphs outside ASCII (guillemets, accents,
    // dashes, arrows). Built once from the generated table.
    private static readonly Dictionary<int, double[]> SansExtra = BuildExtra();

    private static Dictionary<int, double[]> BuildExtra()
    {
        var d = new Dictionary<int, double[]>(InterMetricsData.SansExtra.Length);
        foreach (var (cp, adv) in InterMetricsData.SansExtra) d[cp] = adv;
        return d;
    }

    /// <inheritdoc />
    public TextMetrics Measure(string text, FontRole role)
    {
        FontRoleSpec s = FontRoles.Of(role);
        string t = s.Uppercase ? text.ToUpperInvariant() : text;
        double size = s.SizePx;

        // Monospace: every glyph shares one advance-per-em.
        if (s.Mono)
        {
            int mi = NearestIndex(InterMetricsData.MonoWeights, s.Weight);
            double width = t.Length * InterMetricsData.MonoAdvance[mi] * size;
            width = ApplyLetterSpacing(width, t.Length, s, size);
            return new TextMetrics(width, InterMetricsData.MonoAscent[mi] * size, InterMetricsData.MonoDescent[mi] * size);
        }

        int wi = NearestIndex(InterMetricsData.SansWeights, s.Weight);
        double[] ascii = InterMetricsData.SansAscii[wi];
        double fallback = InterMetricsData.SansFallback[wi];

        double sum = 0;
        foreach (char c in t)
        {
            if (c is >= (char)32 and <= (char)126) sum += ascii[c - 32];
            else if (SansExtra.TryGetValue(c, out double[]? row)) sum += row[wi];
            else sum += fallback;
        }

        double w = ApplyLetterSpacing(sum * size, t.Length, s, size);
        return new TextMetrics(w, InterMetricsData.SansAscent[wi] * size, InterMetricsData.SansDescent[wi] * size);
    }

    // CSS letter-spacing adds a gap after every character (Chrome keeps the trailing gap) —
    // applied identically to SkiaTextMeasurer so the two measurers agree bar the glyph advances.
    private static double ApplyLetterSpacing(double width, int length, FontRoleSpec s, double size) =>
        s.LetterSpacingEm != 0 && length > 0 ? width + length * s.LetterSpacingEm * size : width;

    private static int NearestIndex(int[] weights, int want)
    {
        int best = 0, bestDistance = int.MaxValue;
        for (int i = 0; i < weights.Length; i++)
        {
            int d = Math.Abs(weights[i] - want);
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }
}
