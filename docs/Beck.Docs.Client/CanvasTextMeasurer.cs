using Beck.Rendering.Text;
using Microsoft.JSInterop;

namespace Beck.Docs.Client;

/// <summary>
/// An <see cref="ITextMeasurer"/> for Blazor WebAssembly that measures glyphs with the
/// browser's own 2D-canvas <c>measureText</c> (via the <c>beck-measure.js</c> shim). The
/// browser is the ground truth — it draws the SVG <c>&lt;text&gt;</c> — so this matches the
/// rendered result exactly, the way the original JS engine measured with the live DOM.
///
/// Uses synchronous <see cref="IJSInProcessRuntime"/> calls because a render measures many
/// runs and the pipeline is synchronous; that interface only exists under WebAssembly, which
/// is the only place this measurer runs. Fonts must be loaded first (see the component's
/// first-render hook) or early measurements fall back to a substitute face.
/// </summary>
public sealed class CanvasTextMeasurer : ITextMeasurer
{
    private readonly IJSInProcessRuntime _js;

    public CanvasTextMeasurer(IJSInProcessRuntime js) => _js = js;

    public TextMetrics Measure(string text, FontRole role)
    {
        FontRoleSpec s = FontRoles.Of(role);
        string t = s.Uppercase ? text.ToUpperInvariant() : text;

        // [advanceWidth, ascent, descent] in CSS px.
        double[] m = _js.Invoke<double[]>("beckMeasure.text", t, s.Mono, s.Weight, s.SizePx);
        double width = m[0];

        // CSS letter-spacing adds a gap after every character (Chrome keeps the trailing gap) —
        // applied here, not in the canvas font string, to match SkiaTextMeasurer exactly.
        if (s.LetterSpacingEm != 0 && t.Length > 0)
            width += t.Length * s.LetterSpacingEm * s.SizePx;

        return new TextMetrics(width, m[1], m[2]);
    }
}
