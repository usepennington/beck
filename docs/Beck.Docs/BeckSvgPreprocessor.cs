using System.Net;
using Beck.Rendering;
using Beck.Rendering.Skia;
using Beck.Rendering.Text;
using Pennington.Markdown.Extensions;

namespace Beck.Docs;

/// <summary>
/// Renders a <c>```beck</c> fence to a static, self-animating inline
/// <c>&lt;svg&gt;</c> at build time via the pure-C# <c>Beck.Rendering</c> engine —
/// no client JavaScript, no GSAP. This replaces the runtime <c>beck.global.js</c>
/// hydration for every markdown content diagram (the-bad-idea.md, M10); the JS
/// engine now only backs the interactive playground.
///
/// Text is measured with the site's own IBM Plex Sans + Mono files
/// (<see cref="BeckDocsFonts"/>) so the C# card sizing matches what the browser
/// lays out from the Google-Fonts-loaded families. Theme + palette need no wiring:
/// the emitted SVG keys dark mode off the ancestor <c>[data-theme]</c> the site
/// already toggles, and its <c>--beck-*</c> tokens fall back to the MonorailCSS
/// <c>--color-*</c> ramps, so each diagram adopts the live brand palette for free.
/// </summary>
internal sealed class BeckSvgPreprocessor : ICodeBlockPreprocessor, IDisposable
{
    // Higher runs first; beat the tree-sitter preprocessor (100) so a `beck` fence
    // is never mistaken for a source embed.
    public int Priority => 500;

    private readonly SkiaTextMeasurer _measurer;
    private readonly BeckFontSpec _font;

    public BeckSvgPreprocessor()
    {
        _font = BeckDocsFonts.Spec();
        _measurer = new SkiaTextMeasurer(_font);
    }

    public CodeBlockPreprocessResult? TryProcess(string code, string languageId)
    {
        if (!IsBeck(languageId)) return null; // defer every other fence to the next preprocessor

        string html;
        try
        {
            string svg = BeckSvg.Render(code, new SvgRenderOptions
            {
                Measurer = _measurer,
                Font = _font,
                // Theme: Auto (default) — emits the [data-theme='dark'] hooks the site drives.
                // Animation: Full (default) — flow choreography baked into CSS keyframes.
            });
            html = $"<div class=\"beck-embed\">{svg}</div>";
        }
        catch (Exception ex)
        {
            // A malformed diagram should fail loud (build log + a visible box), never
            // silently vanish or crash the whole page build.
            Console.Error.WriteLine($"[beck] fence render failed: {ex.Message}");
            html = "<div class=\"beck-embed beck-embed--error\"><pre><code>"
                 + WebUtility.HtmlEncode(code) + "</code></pre></div>";
        }

        // SkipTransform: the output is finished HTML; the annotation/highlight pass
        // must not re-process it.
        return new CodeBlockPreprocessResult(html, "beck", SkipTransform: true);
    }

    /// <summary>True when the fence info string's first token is exactly <c>beck</c>.</summary>
    private static bool IsBeck(string languageId)
    {
        ReadOnlySpan<char> s = languageId.AsSpan().Trim();
        int cut = s.IndexOfAny(' ', '\t');
        if (cut >= 0) s = s[..cut];
        return s.Equals("beck", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _measurer.Dispose();
}
