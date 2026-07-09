using System.Collections.Immutable;
using System.Text;
using Beck.Rendering;
using Pennington.Artifacts;
using Pennington.Pipeline;
using Pennington.Routing;

namespace Beck.Docs;

/// <summary>
/// Build-time-rendered gallery fragments for the style guide: one HTML file per built-in style at
/// <c>/fragments/styles/&lt;name&gt;.html</c>, each holding the three shared example diagrams
/// (<c>wwwroot/examples/styles/*.beck.yaml</c>) rendered in that style — the architecture flow
/// ("Read path") fully animated so its arrival pulse shows, the sequence and class renders static.
///
/// The styles guide page (<c>Content/docs/guides/styles.md</c>) places a lightweight
/// <c>.beck-lazy</c> placeholder per style instead of 33 inline SVGs; <c>site.js</c> fetches the
/// matching fragment as the reader scrolls near it and injects the finished markup. This keeps the
/// page payload small while every diagram is still rendered by the C# engine at build time — no
/// client rendering, exactly like the <c>```beck</c> fences (the fragment reuses the fence
/// pipeline's <see cref="BeckSvgPreprocessor.ApplyStyle"/> override and its zoom-button markup, so
/// the lightbox works on injected diagrams too). Same artifact seam as
/// <see cref="RazorPageSocialCardService"/>: served live in dev, baked flat by the static build.
/// </summary>
public sealed class StyleGalleryFragments : IArtifactContentService
{
    /// <summary>The shared example sources, in gallery order, with their animation treatment.
    /// Only the architecture flow animates — one live choreography per style is enough to show
    /// the pulse character without turning the whole gallery into a wall of motion.</summary>
    private static readonly (string File, AnimationMode Animation)[] Examples =
    [
        ("wwwroot/examples/styles/architecture.beck.yaml", AnimationMode.Full),
        ("wwwroot/examples/styles/sequence.beck.yaml", AnimationMode.Static),
        ("wwwroot/examples/styles/class.beck.yaml", AnimationMode.Static),
    ];

    private static readonly IReadOnlyList<string> StyleNames = [.. BeckStyles.ByName.Keys];

    public static string FragmentPath(string style) => $"/fragments/styles/{style}.html";

    private readonly BeckDiagramRenderer _renderer;

    public StyleGalleryFragments(BeckDiagramRenderer renderer) => _renderer = renderer;

    public ImmutableList<ArtifactClaim> Claims { get; } =
        [.. StyleNames.Select(s => new ArtifactClaim(
            $"style-gallery-{s}", new ExactClaim(new UrlPath(FragmentPath(s))), $"{s} style gallery fragment"))];

    public Task<ArtifactContent?> ResolveAsync(string relativePath, CancellationToken cancellationToken)
    {
        string style = Path.GetFileNameWithoutExtension(relativePath);
        if (!relativePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            !BeckStyles.ByName.ContainsKey(style))
        {
            return Task.FromResult<ArtifactContent?>(null);
        }

        var html = new StringBuilder();
        foreach (var (file, animation) in Examples)
        {
            string yaml = BeckSvgPreprocessor.ApplyStyle(
                File.ReadAllText(Path.GetFullPath(file)), style, $"fragment:{style}");
            string svg = _renderer.Render(yaml, animation).Svg;
            html.Append($"<div class=\"beck-embed\">{svg}{BeckSvgPreprocessor.ZoomButton}</div>");
        }
        return Task.FromResult<ArtifactContent?>(
            new ArtifactContent(Encoding.UTF8.GetBytes(html.ToString()), "text/html; charset=utf-8"));
    }

    public async IAsyncEnumerable<DiscoveredItem> DiscoverAsync()
    {
        await Task.CompletedTask;
        foreach (string style in StyleNames)
        {
            yield return new DiscoveredItem(
                new ContentRoute
                {
                    CanonicalPath = new UrlPath(FragmentPath(style)),
                    OutputFile = new FilePath($"fragments/styles/{style}.html"),
                },
                new GeneratedSource("text/html"));
        }
    }
}
