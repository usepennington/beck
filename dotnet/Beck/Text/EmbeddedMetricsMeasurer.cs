namespace Beck.Rendering.Text;

/// <summary>
/// The zero-dependency text measurer, generalized over an embedded <see cref="MetricsTable"/>: it
/// sums per-glyph advance-per-em (per weight) for sans runs and a fixed pitch for mono runs, plus
/// per-em ascent/descent — the same math the classic Inter measurer always used, now table-driven
/// so a style can size against Source Serif, Archivo, or Shantell Sans instead of Inter.
/// </summary>
/// <remarks>
/// Measures against the embedded family regardless of the font the SVG actually asks for, so card
/// sizing is a close <em>approximation</em> — the per-text <c>textLength</c> guard absorbs the small
/// mismatch. For pixel-accurate sizing over a host's real fonts, pass a
/// <c>Beck.Skia.SkiaTextMeasurer</c> on <see cref="SvgRenderOptions.Measurer"/>, which overrides table
/// selection entirely. Advances are a plain per-glyph sum, so kerning and ligatures are not modelled —
/// negligible for the short Latin labels diagrams use. Mono roles always resolve against the shared
/// IBM Plex Mono coverage every table carries.
/// </remarks>
public sealed class EmbeddedMetricsMeasurer : ITextMeasurer
{
    private readonly MetricsTable _t;

    internal EmbeddedMetricsMeasurer(MetricsTable table) => _t = table;

    // One immutable shared instance per built-in font table (the tables are immutable and the
    // measurer is stateless), so repeated renders don't rebuild the SansExtra dictionary.
    private static readonly EmbeddedMetricsMeasurer[] Cache =
        Enum.GetValues<MetricsFont>().Select(f => new EmbeddedMetricsMeasurer(MetricsTables.For(f))).ToArray();

    /// <summary>The shared measurer for a built-in <see cref="MetricsFont"/> table.</summary>
    public static EmbeddedMetricsMeasurer For(MetricsFont font) => Cache[(int)font];

    /// <inheritdoc />
    public TextMetrics Measure(string text, FontRole role)
    {
        FontRoleSpec s = FontRoles.Of(role);
        string t = s.Uppercase ? text.ToUpperInvariant() : text;
        double size = s.SizePx;

        // Monospace: every glyph shares one advance-per-em.
        if (s.Mono)
        {
            int mi = NearestIndex(_t.MonoWeights, s.Weight);
            double width = t.Length * _t.MonoAdvance[mi] * size;
            width = ApplyLetterSpacing(width, t.Length, s, size);
            return new TextMetrics(width, _t.MonoAscent[mi] * size, _t.MonoDescent[mi] * size);
        }

        int wi = NearestIndex(_t.SansWeights, s.Weight);
        double[] ascii = _t.SansAscii[wi];
        double fallback = _t.SansFallback[wi];

        double sum = 0;
        foreach (char c in t)
        {
            if (c is >= (char)32 and <= (char)126) sum += ascii[c - 32];
            else if (_t.SansExtra.TryGetValue(c, out double[]? row)) sum += row[wi];
            else sum += fallback;
        }

        double w = ApplyLetterSpacing(sum * size, t.Length, s, size);
        return new TextMetrics(w, _t.SansAscent[wi] * size, _t.SansDescent[wi] * size);
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
