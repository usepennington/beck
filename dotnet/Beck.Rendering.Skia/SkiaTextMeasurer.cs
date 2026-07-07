using Beck.Rendering.Text;

namespace Beck.Rendering.Skia;

/// <summary>
/// Exact text measurement via SkiaSharp typefaces + HarfBuzzSharp shaping, so
/// ligatures and kerning contribute to the advance sum. Constructed from a
/// <see cref="BeckFontSpec"/>; pass the same spec to
/// <see cref="SvgRenderOptions.Font"/> so the SVG asks for the measured font.
/// </summary>
/// <remarks>The shaping implementation lands in milestone M2.</remarks>
public sealed class SkiaTextMeasurer : ITextMeasurer
{
    private readonly BeckFontSpec _spec;

    public SkiaTextMeasurer(BeckFontSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _spec = spec;
    }

    /// <inheritdoc />
    public TextMetrics Measure(string text, FontRole role) =>
        throw new NotImplementedException("SkiaTextMeasurer is implemented in milestone M2.");
}
