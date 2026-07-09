using Ashcroft;
using Pennington.SocialCards;

namespace Beck.Docs;

/// <summary>
/// Composes the per-page social-card PNG (og:image) Pennington's <c>SocialCards</c> hook asks
/// for: the page title and description drawn over <c>assets/social-card-art.png</c> — the site's
/// dark dot-grid surface with a real rendered Beck diagram ghosted along the right edge, leaving
/// the left side free for text. The art is baked once in a browser for full CSS fidelity (the
/// engine's SVG output styles itself via <c>var(--beck-*)</c>/<c>color-mix()</c>, beyond any
/// C#-side SVG rasterizer); <c>assets/README.md</c> has the regeneration recipe.
/// Typography and palette mirror the site brand: IBM Plex over slate, emerald accent.
/// </summary>
internal static class SocialCardGenerator
{
    private static string Asset(string file) => Path.Combine(AppContext.BaseDirectory, "assets", file);

    // The hero tagline, used when a page carries no description of its own (and as the
    // headline for the home page, whose "title" is just the site name).
    private const string Tagline = "Beautiful animated architecture diagrams from declarative YAML. Pure SVG out — no JavaScript.";

    /// <summary>Pennington's per-content-page hook (markdown pages).</summary>
    public static Task<byte[]?> Build(SocialCardRequest request)
    {
        var isHome = string.IsNullOrWhiteSpace(request.Title)
                     || string.Equals(request.Title, request.SiteTitle, StringComparison.OrdinalIgnoreCase);
        return Task.FromResult<byte[]?>(Compose(
            isHome ? null : request.Title,
            request.Description,
            request.Width,
            request.Height));
    }

    /// <summary>
    /// Draws one card: brand eyebrow + headline + description over the baked art. A null
    /// <paramref name="headline"/> means "the site itself" (the home page and other pages
    /// with no title of their own) and falls back to the hero pitch.
    /// </summary>
    public static byte[] Compose(string? headline, string? description, int width = 1200, int height = 630)
    {
        var fonts = BeckDocsFonts.Spec();
        if (headline is null)
        {
            headline = "Animated architecture diagrams";
            description ??= "Declarative YAML in, a self-animating SVG out. No JavaScript.";
        }
        description = description is { Length: > 0 } ? description : Tagline;

        return SocialCard.Create(width, height)
            .Background(Asset("social-card-art.png"))
            .NoScrim() // the art is already dark; legibility is baked into its left-side fade
            .Theme(new Theme
            {
                FontFiles =
                [
                    fonts.Files[400],
                    fonts.Files[700],
                    fonts.MonoFiles[500],
                ],
            })
            .At(Anchor.MiddleLeft, s => s
                .MaxWidth(600)
                .Text("BECK", new TextStyle
                {
                    FontFamily = fonts.MonoFamily, Size = 22, Weight = 500,
                    Color = "#34d399", LetterSpacing = 4,
                })
                .Spacer(10)
                .Text(headline, new TextStyle
                {
                    FontFamily = fonts.Family, Size = 62, Weight = 700,
                    Color = "#f8fafc", LineHeight = 1.08f, MaxLines = 3, ShrinkToFit = true,
                })
                .Spacer(8)
                .Text(description, new TextStyle
                {
                    FontFamily = fonts.Family, Size = 27, Weight = 400,
                    Color = "#94a3b8", LineHeight = 1.35f, MaxLines = 3,
                }))
            .ToBytes(ImageFormat.Png);
    }
}
