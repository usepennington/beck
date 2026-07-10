namespace Beck.Text;

/// <summary>
/// The default, zero-dependency text measurer: Inter (sans) + IBM Plex Mono embedded metrics. Kept
/// as the named default (<see cref="SvgRenderOptions.Measurer"/> points at <see cref="Instance"/>)
/// and the reference identity for byte-identical classic output; it delegates to the generalized
/// <see cref="EmbeddedMetricsMeasurer"/> over the <see cref="MetricsFont.Inter"/> table, which now
/// carries the measurement math for every embedded family.
/// </summary>
/// <remarks>
/// This measures against Inter/Plex regardless of the font the SVG actually asks for, so card
/// sizing is a close <em>approximation</em> — the per-text <c>textLength</c> guard absorbs the
/// small mismatch. For pixel-accurate sizing over your site's real fonts, pass a
/// <c>Beck.Skia.SkiaTextMeasurer</c> (see the install guide).
/// </remarks>
public sealed class InterMetricsMeasurer : ITextMeasurer
{
    /// <summary>The shared, immutable default measurer.</summary>
    public static InterMetricsMeasurer Instance { get; } = new();

    private readonly EmbeddedMetricsMeasurer _impl = EmbeddedMetricsMeasurer.For(MetricsFont.Inter);

    private InterMetricsMeasurer() { }

    /// <inheritdoc />
    public TextMetrics Measure(string text, FontRole role) => _impl.Measure(text, role);

    /// <inheritdoc />
    public TextMetrics Measure(string text, FontRole role, FontRoleSpec spec) => _impl.Measure(text, spec);
}
