using Beck.Rendering;
using Beck.Rendering.Svg;
using Xunit;
using BeckStyle = Beck.BeckStyle;

namespace Beck.Tests;

/// <summary>
/// Phase-1 gate for the BeckStyle refactor: the extraction is a pure plumbing change, so the
/// default render path (which resolves the style internally to <see cref="BeckStyle.Classic"/>)
/// must be byte-for-byte identical to threading <see cref="BeckStyle.Classic"/> explicitly through
/// the internal <see cref="SvgRenderer.Render"/> seam — including a <c>with</c>-copy of Classic,
/// proving the record indirection is transparent. Uses the default managed measurer (no Skia /
/// TestFonts coupling): this exercises plumbing, not measurement. Covers every corpus file and the
/// theme/animation option axes.
/// </summary>
public sealed class StyleByteIdentityTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

    public static IEnumerable<object[]> Corpus() =>
        Directory.EnumerateFiles(CorpusDir, "*.yaml").Select(f => new object[] { Path.GetFileName(f) });

    private static readonly SvgRenderOptions[] Variants =
    {
        new(),
        new() { Animation = AnimationMode.Static },
        new() { Animation = AnimationMode.Scrub },
        new() { Theme = Beck.Rendering.ThemeMode.Dark },
        new() { Theme = Beck.Rendering.ThemeMode.Light },
    };

    [Theory]
    [MemberData(nameof(Corpus))]
    public void DefaultPath_EqualsExplicitClassic(string file)
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, file));

        foreach (SvgRenderOptions options in Variants)
        {
            string viaDefault = BeckSvg.Render(yaml, options);

            // Re-render through the internal seam with an explicit Classic (and a with-copy of it),
            // reproducing exactly what RenderWithInfo does around the style-resolution point.
            string hash = BeckSvg.ResolveIdSuffix(yaml, options);
            DiagramModel model = Validate.LoadDiagram(yaml);
            string viaClassic = SvgRenderer.Render(model, options.Measurer, hash, options, BeckStyle.Classic);
            string viaCopy = SvgRenderer.Render(model, options.Measurer, hash, options, BeckStyle.Classic with { });

            Assert.Equal(viaDefault, viaClassic);
            Assert.Equal(viaDefault, viaCopy);
        }
    }
}
