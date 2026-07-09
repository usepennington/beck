using System.Net;
using Beck;
using Beck.Rendering;
using Pennington.Markdown.Extensions;
using YamlDotNet.RepresentationModel;

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
/// <para>
/// <c>```beck,style=sketch</c> (or <c>```beck:symbol,style=sketch</c>) injects/overrides
/// <c>meta.style</c> on the document before rendering, so one shared YAML snippet can be
/// shown in every built-in look without hand-editing eleven copies (this is what the style
/// gallery uses). The flag <em>wins</em> over the fence's own <c>meta.style</c> — it is a
/// last-word override, unlike the C# <c>SvgRenderOptions.Style</c> site-wide default that a
/// document opts out of. An unknown style token fails loud (build-log warning) and the fence
/// renders with the document's own style unchanged.
/// </para>
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

        // A `beck:symbol` fence body may name several files; each is its own document and
        // must render (and fail) independently — one malformed file must not drop the rest.
        List<string> yamls;
        try
        {
            yamls = fence.IsFileEmbed ? ReadEmbeddedYaml(code) : [code];
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[beck] fence render failed ({languageId}): {ex.Message}");
            return new CodeBlockPreprocessResult(
                "<div class=\"beck-embed beck-embed--error\"><pre><code>"
                    + WebUtility.HtmlEncode(code) + "</code></pre></div>",
                "beck", SkipTransform: true);
        }
        var html = string.Concat(yamls.Select(yaml => RenderOne(yaml, fence, languageId)));

        // SkipTransform: the output is finished HTML; the annotation/highlight pass
        // must not re-process it.
        return new CodeBlockPreprocessResult(html, "beck", SkipTransform: true);
    }

    private string RenderOne(string yaml, FenceInfo fence, string languageId)
    {
        try
        {
            if (fence.StyleName is { } style) yaml = ApplyStyle(yaml, style, languageId);
            string svg = _renderer.Render(yaml, fence.Animation).Svg;
            return $"<div class=\"beck-embed\">{svg}{ZoomButton}</div>";
        }
        catch (Exception ex)
        {
            // A malformed diagram (or a missing embed file) should fail loud (build log +
            // a visible box), never silently vanish or crash the whole page build. Show the
            // failing document's own YAML, not the fence body (which for `:symbol` is file paths).
            Console.Error.WriteLine($"[beck] fence render failed ({languageId}): {ex.Message}");
            return "<div class=\"beck-embed beck-embed--error\"><pre><code>"
                 + WebUtility.HtmlEncode(yaml) + "</code></pre></div>";
        }
    }

    /// <summary>
    /// Fullscreen-zoom affordance emitted after the SVG inside every successful embed. site.js
    /// listens for clicks on <c>.beck-zoom</c> (delegated, so it survives Blazor navigation) and
    /// opens a <c>&lt;dialog class="beck-lightbox"&gt;</c> with a clone of the diagram over a
    /// dimmed, blurred backdrop; BrandStyling.cs carries the CSS for both. The icon is an inline
    /// expand glyph so the button needs no asset.
    /// </summary>
    internal const string ZoomButton =
        "<button class=\"beck-zoom\" type=\"button\" aria-label=\"View diagram full screen\" title=\"View full screen\">"
        + "<svg viewBox=\"0 0 24 24\" width=\"14\" height=\"14\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">"
        + "<path d=\"M15 3h6v6M9 21H3v-6M21 3l-7 7M3 21l7-7\"/></svg></button>";

    /// <summary>
    /// Rewrites every document in <paramref name="yaml"/> so its <c>meta.style</c> is
    /// <paramref name="styleName"/> — the <c>style=</c> fence flag as a last-word override of
    /// whatever the document itself declares. An unknown style token warns to the build log and
    /// leaves the YAML untouched (the fence renders with its own style), matching how the rest of
    /// this preprocessor fails loud rather than silently. Editing the parsed representation graph
    /// (rather than a text splice) keeps this correct whether <c>meta</c> is block- or flow-styled,
    /// present or absent, in a single- or multi-document body.
    /// </summary>
    internal static string ApplyStyle(string yaml, string styleName, string languageId)
    {
        if (!BeckStyles.ByName.ContainsKey(styleName))
        {
            Console.Error.WriteLine(
                $"[beck] unknown style \"{styleName}\" in fence `{languageId}` — expected one of "
                + $"{string.Join(", ", BeckStyles.ByName.Keys)}. Rendering with the document's own style.");
            return yaml;
        }

        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0) return yaml;

        foreach (var doc in stream.Documents)
        {
            if (doc.RootNode is not YamlMappingNode root) continue;
            var metaKey = new YamlScalarNode("meta");
            if (root.Children.TryGetValue(metaKey, out var node) && node is YamlMappingNode meta)
                meta.Children[new YamlScalarNode("style")] = new YamlScalarNode(styleName);
            else
                root.Children[metaKey] = new YamlMappingNode(
                    new YamlScalarNode("style"), new YamlScalarNode(styleName));
        }

        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    /// <summary>
    /// Reads the YAML a <c>beck:symbol</c> fence points at. The body is one file path per
    /// line (a trailing <c>" &gt; symbol"</c> selector is ignored — whole-file YAML has no
    /// symbols), resolved with <see cref="Path.GetFullPath(string)"/> so it matches the
    /// working-directory resolution the tree-sitter <c>:symbol</c> embed uses
    /// (<c>ContentRoot = "."</c>). Multiple paths each render as their own diagram (own
    /// <c>.beck-embed</c>, own zoom button) — the caller renders every returned document
    /// independently so one bad file surfaces its own error box instead of dropping the rest.
    /// </summary>
    private static List<string> ReadEmbeddedYaml(string code)
    {
        var paths = code
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(StripSelector)
            .Where(p => p.Length > 0)
            .ToList();
        if (paths.Count == 0)
            throw new InvalidOperationException("beck:symbol fence has no file path in its body.");

        return paths.Select(p => File.ReadAllText(Path.GetFullPath(p))).ToList();
    }

    /// <summary>Drops a <c>" &gt; member"</c> tail from a source reference, leaving the file path.</summary>
    private static string StripSelector(string line)
    {
        int cut = line.IndexOf(" > ", StringComparison.Ordinal);
        return (cut < 0 ? line : line[..cut]).Trim();
    }

    /// <summary>
    /// Parses a fence info-string such as <c>beck</c>, <c>beck:symbol</c>,
    /// <c>beck:symbol,static</c>, or <c>beck,style=sketch</c> into whether it is a Beck fence,
    /// whether the body is a file path (<c>:symbol</c>), the resolved <see cref="AnimationMode"/>,
    /// and an optional <c>style=</c> override. Non-Beck fences return <see cref="FenceInfo.IsBeck"/>
    /// false. Every token after the language is comma-separated — the historical <c>:symbol</c>
    /// modifier and the comma flag tail are parsed uniformly, so flags work on the inline form
    /// (<c>beck,static</c>) exactly as on the file-embed form (<c>beck:symbol,static</c>).
    /// </summary>
    private static FenceInfo ParseInfo(string languageId)
    {
        ReadOnlySpan<char> s = languageId.AsSpan().Trim();
        int ws = s.IndexOfAny(' ', '\t');
        if (ws >= 0) s = s[..ws];

        var segments = s.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) return FenceInfo.NotBeck;

        // The head segment is `beck` or `beck:<modifier>`; split off the colon modifier.
        ReadOnlySpan<char> head = segments[0].AsSpan();
        int colon = head.IndexOf(':');
        ReadOnlySpan<char> baseLang = colon >= 0 ? head[..colon] : head;
        if (!baseLang.Equals("beck", StringComparison.OrdinalIgnoreCase))
            return FenceInfo.NotBeck;

        // Flags = the colon modifier (if any) followed by every comma segment. Parsing them
        // through one path keeps `beck:symbol`, `beck:symbol,static`, and `beck,style=x` uniform.
        bool fileEmbed = false;
        var animation = AnimationMode.Full;
        string? styleName = null;

        if (colon >= 0) Apply(head[(colon + 1)..].ToString());
        for (int i = 1; i < segments.Length; i++) Apply(segments[i]);

        void Apply(string flag)
        {
            if (flag.Equals("symbol", StringComparison.OrdinalIgnoreCase)) fileEmbed = true;
            else if (flag.Equals("static", StringComparison.OrdinalIgnoreCase)) animation = AnimationMode.Static;
            else if (flag.Equals("scrub", StringComparison.OrdinalIgnoreCase)) animation = AnimationMode.Scrub;
            else if (flag.StartsWith("style=", StringComparison.OrdinalIgnoreCase))
                styleName = flag["style=".Length..].Trim();
            // unknown flags are ignored, matching the tree-sitter flag parser
        }

        return new FenceInfo(true, fileEmbed, animation, styleName);
    }

    private readonly record struct FenceInfo(bool IsBeck, bool IsFileEmbed, AnimationMode Animation, string? StyleName)
    {
        public static readonly FenceInfo NotBeck = new(false, false, AnimationMode.Full, null);
    }
}
