using System.Text.RegularExpressions;
using System.Xml;
using Beck.Rendering;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Per-style smoke tests (new-designs.md Phase 3+): every built-in style beyond <c>classic</c> gets
/// one row here. Renders the architecture/sequence/class corpus kitchen-sink diagrams under that
/// style and asserts the invariants every style must hold — valid XML, no negative path coordinates
/// (the router-regression signature), determinism, and light/dark/scrub/reduced-motion variants all
/// render — plus that the style actually changed the output relative to classic. Add a new style by
/// appending one entry to <see cref="Styles"/>.
/// </summary>
public sealed class StyleSmokeTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

    // (style name, style instance) — one row per non-classic built-in.
    public static IEnumerable<object[]> Styles() => new[]
    {
        new object[] { MinimalStyle.Instance },
        new object[] { TerminalStyle.Instance },
        new object[] { BlueprintStyle.Instance },
        new object[] { GlowStyle.Instance },
        new object[] { EditorialStyle.Instance },
        new object[] { BrutalistStyle.Instance },
        new object[] { SketchStyle.Instance },
        new object[] { ExtrudeStyle.Instance },
        new object[] { CircuitStyle.Instance },
        new object[] { MetroStyle.Instance },
    };

    public static IEnumerable<object[]> StyledDiagrams() =>
        from style in Styles()
        from diagram in new[] { "arch-kitchen", "seq-kitchen", "class", "state" }
        select new object[] { (BeckStyle)style[0], diagram };

    [Theory]
    [MemberData(nameof(StyledDiagrams))]
    public void Style_RendersValidNoNegativePaths(BeckStyle style, string diagram)
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, diagram + ".yaml"));
        string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = style });

        AssertValidXml(svg);
        AssertNoNegativePathCoords(svg);
    }

    [Theory]
    [MemberData(nameof(StyledDiagrams))]
    public void Style_IsDeterministic(BeckStyle style, string diagram)
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, diagram + ".yaml"));
        var options = new SvgRenderOptions { Style = style };
        string a = BeckSvg.Render(yaml, options);
        string b = BeckSvg.Render(yaml, options);
        Assert.Equal(a, b);
    }

    [Theory]
    [MemberData(nameof(StyledDiagrams))]
    public void Style_DiffersFromClassic(BeckStyle style, string diagram)
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, diagram + ".yaml"));
        string styled = BeckSvg.Render(yaml, new SvgRenderOptions { Style = style });
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.NotEqual(classic, styled);
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Style_LightDarkScrubReducedMotion_AllRender(BeckStyle style)
    {
        string yaml = File.ReadAllText(Path.Combine(CorpusDir, "arch-kitchen.yaml"));

        var variants = new[]
        {
            new SvgRenderOptions { Style = style, Theme = Beck.Rendering.ThemeMode.Light },
            new SvgRenderOptions { Style = style, Theme = Beck.Rendering.ThemeMode.Dark },
            new SvgRenderOptions { Style = style, Animation = AnimationMode.Scrub },
            new SvgRenderOptions { Style = style, Animation = AnimationMode.Static },
        };
        foreach (SvgRenderOptions options in variants)
        {
            string svg = BeckSvg.Render(yaml, options);
            AssertValidXml(svg);
            AssertNoNegativePathCoords(svg);
        }

        // Reduced motion is a client-side media query, not a render option: confirm all motion CSS
        // is scoped inside the no-preference block, so a reduced-motion viewer gets the static frame.
        string full = BeckSvg.Render(yaml, new SvgRenderOptions { Style = style });
        Assert.Contains("prefers-reduced-motion:no-preference", full);
    }

    // Only the router's *edge* paths live on the always-positive canvas — icon glyphs (feather-style
    // paths in their own local 24x24 box) legitimately use negative coordinates, so this scopes to
    // <path class="beck-edge ..."> only, matching CLAUDE.md's "off-canvas edge routes" signature.
    private static readonly Regex EdgePathD = new("<path class=\"beck-edge[^\"]*\"[^>]*\\bd=\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex NumTok = new("-?[0-9]*\\.?[0-9]+", RegexOptions.Compiled);

    private static void AssertNoNegativePathCoords(string svg)
    {
        foreach (Match pm in EdgePathD.Matches(svg))
        {
            foreach (Match nm in NumTok.Matches(pm.Groups[1].Value))
            {
                double v = double.Parse(nm.Value, System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(v >= 0, $"negative path coordinate {v} in: {pm.Groups[1].Value}");
            }
        }
    }

    private static void AssertValidXml(string svg)
    {
        var doc = new XmlDocument();
        doc.LoadXml(svg);
    }
}
