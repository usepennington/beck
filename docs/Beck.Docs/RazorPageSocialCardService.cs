using System.Collections.Immutable;
using Pennington.Artifacts;
using Pennington.Pipeline;
using Pennington.Routing;

namespace Beck.Docs;

/// <summary>
/// Social cards for the hand-built Razor pages (home, playground, API, syntax), which live
/// outside the markdown pipeline and so never reach Pennington's own <c>SocialCards</c>
/// discovery. Served under <c>/og/&lt;key&gt;.png</c> (a distinct prefix from Pennington's
/// <c>/social-cards/</c> claim) in dev and baked into the static build; each page points at
/// its card via <see cref="Components.SocialMeta"/>. Same
/// <see cref="SocialCardGenerator.Compose"/> pipeline as the markdown-page cards.
/// </summary>
public sealed class RazorPageSocialCardService : IArtifactContentService
{
    /// <summary>Card copy per Razor page. The home entry's null headline selects the hero pitch.</summary>
    internal static readonly IReadOnlyDictionary<string, (string? Title, string? Description)> Pages =
        new Dictionary<string, (string?, string?)>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = (null, null),
            ["playground"] = ("Playground",
                "Edit Beck YAML in the browser and watch the diagram render live — the same C# engine, compiled to WebAssembly."),
            ["api"] = ("API reference",
                "The fluent DiagramBuilder authoring API — emit Beck YAML straight from C#."),
            ["syntax"] = ("Syntax cheatsheet",
                "Every Beck YAML feature on one page, each with a live rendered diagram."),
        };

    public static string CardPath(string key) => $"/og/{key}.png";

    public ImmutableList<ArtifactClaim> Claims { get; } =
        [.. Pages.Keys.Select(k => new ArtifactClaim($"og-{k}", new ExactClaim(new UrlPath(CardPath(k))), $"{k} social card"))];

    public Task<ArtifactContent?> ResolveAsync(string relativePath, CancellationToken cancellationToken)
    {
        var file = Path.GetFileNameWithoutExtension(relativePath);
        if (!relativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            !Pages.TryGetValue(file, out var page))
        {
            return Task.FromResult<ArtifactContent?>(null);
        }

        var bytes = SocialCardGenerator.Compose(page.Title, page.Description);
        return Task.FromResult<ArtifactContent?>(new ArtifactContent(bytes, "image/png"));
    }

    public async IAsyncEnumerable<DiscoveredItem> DiscoverAsync()
    {
        await Task.CompletedTask;
        foreach (var key in Pages.Keys)
        {
            yield return new DiscoveredItem(
                new ContentRoute
                {
                    CanonicalPath = new UrlPath(CardPath(key)),
                    OutputFile = new FilePath($"og/{key}.png"),
                },
                new GeneratedSource("image/png"));
        }
    }
}