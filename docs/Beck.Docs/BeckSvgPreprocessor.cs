using System.Net;
using Beck.Rendering;
using Pennington.Markdown.Extensions;

namespace Beck.Docs;

/// <summary>
/// Renders a <c>```beck</c> fence to a static, self-animating inline
/// <c>&lt;svg&gt;</c> at build time via the pure-C# Beck engine — no client
/// JavaScript, no GSAP. Every markdown content diagram renders this way; the
/// interactive playground runs the same C# engine compiled to WebAssembly.
///
/// Two fence forms are supported:
/// <list type="bullet">
///   <item><c>```beck</c> — the fence body is inline Beck YAML, rendered directly.</item>
///   <item><c>```beck:symbol</c> — the body is a file path (resolved exactly like the
///     sibling <c>```yaml:symbol</c> source embed, relative to the working directory),
///     whose YAML is read and rendered. This lets a diagram live in one shared
///     <c>.beck.yaml</c> and appear as both highlighted source and a live render from
///     that single file — the DRY convention the guides use, replacing
///     <c>&lt;beck-diagram src="…"&gt;</c>.</item>
/// </list>
/// A comma-separated flag tail after the modifier tunes the render:
/// <c>```beck:symbol,static</c> forces the fully-revealed static frame (the old
/// <c>animate="false"</c>), and <c>,scrub</c> drives the choreography from scroll
/// position. Flags work on the inline form too (<c>```beck,static</c>).
///
/// Text is measured with the site's own IBM Plex Sans + Mono files
/// (<see cref="BeckDocsFonts"/>) so the C# card sizing matches what the browser
/// lays out from the Google-Fonts-loaded families. Theme + palette need no wiring:
/// the emitted SVG keys dark mode off the ancestor <c>[data-theme]</c> the site
/// already toggles, and its <c>--beck-*</c> tokens fall back to the MonorailCSS
/// <c>--color-*</c> ramps, so each diagram adopts the live brand palette for free.
/// </summary>
internal sealed class BeckSvgPreprocessor : ICodeBlockPreprocessor
{
    // Higher runs first; beat the tree-sitter preprocessor (100) so a `beck` fence
    // (including `beck:symbol`) is handled here, never mistaken for a source embed.
    public int Priority => 500;

    private readonly BeckDiagramRenderer _renderer;

    public BeckSvgPreprocessor(BeckDiagramRenderer renderer) => _renderer = renderer;

    public CodeBlockPreprocessResult? TryProcess(string code, string languageId)
    {
        var fence = ParseInfo(languageId);
        if (!fence.IsBeck) return null; // defer every other fence to the next preprocessor

        string html;
        try
        {
            string yaml = fence.IsFileEmbed ? ReadEmbeddedYaml(code) : code;
            string svg = _renderer.Render(yaml, fence.Animation).Svg;
            html = $"<div class=\"beck-embed\">{svg}</div>";
        }
        catch (Exception ex)
        {
            // A malformed diagram (or a missing embed file) should fail loud (build log +
            // a visible box), never silently vanish or crash the whole page build.
            Console.Error.WriteLine($"[beck] fence render failed ({languageId}): {ex.Message}");
            html = "<div class=\"beck-embed beck-embed--error\"><pre><code>"
                 + WebUtility.HtmlEncode(code) + "</code></pre></div>";
        }

        // SkipTransform: the output is finished HTML; the annotation/highlight pass
        // must not re-process it.
        return new CodeBlockPreprocessResult(html, "beck", SkipTransform: true);
    }

    /// <summary>
    /// Reads the YAML a <c>beck:symbol</c> fence points at. The body is one file path per
    /// line (a trailing <c>" &gt; symbol"</c> selector is ignored — whole-file YAML has no
    /// symbols), resolved with <see cref="Path.GetFullPath(string)"/> so it matches the
    /// working-directory resolution the tree-sitter <c>:symbol</c> embed uses
    /// (<c>ContentRoot = "."</c>). Multiple paths render as stacked diagrams.
    /// </summary>
    private static string ReadEmbeddedYaml(string code)
    {
        var paths = code
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(StripSelector)
            .Where(p => p.Length > 0)
            .ToList();
        if (paths.Count == 0)
            throw new InvalidOperationException("beck:symbol fence has no file path in its body.");
        if (paths.Count == 1)
            return File.ReadAllText(Path.GetFullPath(paths[0]));

        // Rare multi-file case: join documents so each renders (the caller wraps the lot).
        return string.Join("\n---\n", paths.Select(p => File.ReadAllText(Path.GetFullPath(p))));
    }

    /// <summary>Drops a <c>" &gt; member"</c> tail from a source reference, leaving the file path.</summary>
    private static string StripSelector(string line)
    {
        int cut = line.IndexOf(" > ", StringComparison.Ordinal);
        return (cut < 0 ? line : line[..cut]).Trim();
    }

    /// <summary>
    /// Parses a fence info-string such as <c>beck</c>, <c>beck:symbol</c>, or
    /// <c>beck:symbol,static</c> into whether it is a Beck fence, whether the body is a
    /// file path (<c>:symbol</c>), and the resolved <see cref="AnimationMode"/> from the
    /// comma-separated flag tail. Non-Beck fences return <see cref="FenceInfo.IsBeck"/> false.
    /// </summary>
    private static FenceInfo ParseInfo(string languageId)
    {
        ReadOnlySpan<char> s = languageId.AsSpan().Trim();
        int ws = s.IndexOfAny(' ', '\t');
        if (ws >= 0) s = s[..ws];

        int colon = s.IndexOf(':');
        ReadOnlySpan<char> baseLang = colon >= 0 ? s[..colon] : s;
        if (!baseLang.Equals("beck", StringComparison.OrdinalIgnoreCase))
            return FenceInfo.NotBeck;

        bool fileEmbed = false;
        var animation = AnimationMode.Full;
        if (colon >= 0)
        {
            foreach (var range in s[(colon + 1)..].ToString()
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (range.Equals("symbol", StringComparison.OrdinalIgnoreCase)) fileEmbed = true;
                else if (range.Equals("static", StringComparison.OrdinalIgnoreCase)) animation = AnimationMode.Static;
                else if (range.Equals("scrub", StringComparison.OrdinalIgnoreCase)) animation = AnimationMode.Scrub;
                // unknown tokens are ignored, matching the tree-sitter flag parser
            }
        }

        return new FenceInfo(true, fileEmbed, animation);
    }

    private readonly record struct FenceInfo(bool IsBeck, bool IsFileEmbed, AnimationMode Animation)
    {
        public static readonly FenceInfo NotBeck = new(false, false, AnimationMode.Full);
    }
}
