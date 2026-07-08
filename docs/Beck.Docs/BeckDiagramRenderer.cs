using Beck.Rendering;
using Beck.Skia;
using Beck.Rendering.Text;

namespace Beck.Docs;

/// <summary>
/// The docs host's shared Beck renderer: one <see cref="SkiaTextMeasurer"/> over the site's
/// own IBM Plex fonts (<see cref="BeckDocsFonts"/>), so C# card sizing matches what the browser
/// lays out from the Google-Fonts families. Registered as a singleton and reused by both the
/// build-time <c>```beck</c> fence preprocessor (<see cref="BeckSvgPreprocessor"/>) and the
/// interactive Blazor playground — sharing one measurer avoids loading the font files twice.
///
/// Safe to share across concurrent requests and interactive circuits: the measurer serialises
/// its HarfBuzz shaping internally, and a render is otherwise a pure function of its input.
/// </summary>
public sealed class BeckDiagramRenderer : IDisposable
{
    private readonly SkiaTextMeasurer _measurer;
    private readonly BeckFontSpec _font;

    public BeckDiagramRenderer()
    {
        _font = BeckDocsFonts.Spec();
        _measurer = new SkiaTextMeasurer(_font);
    }

    /// <summary>Render options wired to the shared measurer + brand fonts for the given animation mode.</summary>
    public SvgRenderOptions Options(AnimationMode animation = AnimationMode.Full) => new()
    {
        Measurer = _measurer,
        Font = _font,
        Animation = animation,
        // Theme: Auto (default) — emits the [data-theme='dark'] hooks the site drives.
    };

    /// <summary>Render <paramref name="yaml"/> to an SVG plus node/edge counts for the status readout.</summary>
    public BeckRenderInfo Render(string yaml, AnimationMode animation = AnimationMode.Full) =>
        BeckSvg.RenderWithInfo(yaml, Options(animation));

    public void Dispose() => _measurer.Dispose();
}
