namespace Beck.Rendering.Text;

/// <summary>
/// The default, zero-dependency text measurer. Backed by an embedded metrics
/// table generated offline from Inter and IBM Plex Mono (per-glyph advance widths
/// per weight, plus ascent/descent ratios). Used whenever the caller does not
/// supply a <see cref="BeckFontSpec"/>-backed exact measurer.
/// </summary>
/// <remarks>
/// The embedded table and the box-model sizing are built in milestone M2; this
/// scaffold exposes the singleton so <see cref="SvgRenderOptions"/> can default
/// to it.
/// </remarks>
public sealed class InterMetricsMeasurer : ITextMeasurer
{
    /// <summary>The shared, immutable default measurer.</summary>
    public static InterMetricsMeasurer Instance { get; } = new();

    private InterMetricsMeasurer() { }

    /// <inheritdoc />
    public TextMetrics Measure(string text, FontRole role) =>
        throw new NotImplementedException("InterMetricsMeasurer is implemented in milestone M2.");
}
