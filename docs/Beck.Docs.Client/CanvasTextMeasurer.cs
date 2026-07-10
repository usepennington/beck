using Beck.Text;
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

    /// <inheritdoc />
    public TextMetrics Measure(string text, FontRole role) => Measure(text, role, FontRoles.Of(role));

    /// <summary>
    /// Style-aware measurement: measures against the <em>active style's</em> resolved
    /// <paramref name="spec"/> (weight / size / mono-vs-sans family / letter-spacing / uppercase)
    /// rather than the classic <see cref="FontRoles.Of"/> for the role. Without this override a style
    /// that remaps a role (terminal's uppercase mono titles, brutalist's heavier/larger weights,
    /// editorial's serif) would be sized against the classic role and rely on the <c>textLength</c>
    /// guard to squeeze the glyphs back into a mis-sized box; honouring the spec sizes the card to
    /// what the browser actually draws.
    /// </summary>
    public TextMetrics Measure(string text, FontRole role, FontRoleSpec spec)
    {
        var t = spec.Uppercase ? text.ToUpperInvariant() : text;

        // [advanceWidth, ascent, descent] in CSS px. mono/weight/size come from the active style's
        // spec, so a role a style remapped measures at its rendered typography.
        var m = _js.Invoke<double[]>("beckMeasure.text", t, spec.Mono, spec.Weight, spec.SizePx);
        var width = m[0];

        // CSS letter-spacing adds a gap after every character (Chrome keeps the trailing gap) —
        // applied here, not in the canvas font string, to match SkiaTextMeasurer exactly.
        if (spec.LetterSpacingEm != 0 && t.Length > 0)
        {
            width += t.Length * spec.LetterSpacingEm * spec.SizePx;
        }

        return new TextMetrics(width, m[1], m[2]);
    }
}