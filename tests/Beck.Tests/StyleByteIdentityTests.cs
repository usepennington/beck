using Beck.Model;
using Beck.Svg;
using Xunit;

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
    private static readonly string _corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

    public static IEnumerable<object[]> Corpus() =>
        Directory.EnumerateFiles(_corpusDir, "*.yaml").Select(f => new object[] { Path.GetFileName(f) });

    private static readonly SvgRenderOptions[] _variants =
    [
        new(),
        new() { Animation = AnimationMode.Static },
        new() { Animation = AnimationMode.Scrub },
        new() { Theme = ThemeMode.Dark },
        new() { Theme = ThemeMode.Light },
    ];

    [Theory]
    [MemberData(nameof(Corpus))]
    public void DefaultPath_EqualsExplicitClassic(string file)
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, file));

        foreach (var options in _variants)
        {
            var viaDefault = BeckSvg.Render(yaml, options);

            // Re-render through the internal seam with an explicit Classic (and a with-copy of it),
            // reproducing exactly what RenderWithInfo does around the style-resolution point.
            var hash = BeckSvg.ResolveIdSuffix(yaml, options);
            var model = Validate.LoadDiagram(yaml);
            var viaClassic = SvgRenderer.Render(model, options.Measurer, hash, options, BeckStyle.Classic);
            var viaCopy = SvgRenderer.Render(model, options.Measurer, hash, options, BeckStyle.Classic with { });

            Assert.Equal(viaDefault, viaClassic);
            Assert.Equal(viaDefault, viaCopy);
        }
    }
}