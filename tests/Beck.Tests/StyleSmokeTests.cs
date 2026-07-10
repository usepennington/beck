using System.Text.RegularExpressions;
using System.Xml;
using Beck.Styles;
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
    private static readonly string _corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");

    // (style name, style instance) — one row per non-classic built-in.
    public static IEnumerable<object[]> Styles() =>
    [
        [MinimalStyle.Instance],
        [TerminalStyle.Instance],
        [BlueprintStyle.Instance],
        [GlowStyle.Instance],
        [BrutalistStyle.Instance],
        [SketchStyle.Instance],
        [ExtrudeStyle.Instance],
        [CircuitStyle.Instance],
    ];

    public static IEnumerable<object[]> StyledDiagrams() =>
        from style in Styles()
        from diagram in new[] { "arch-kitchen", "seq-kitchen", "class", "state" }
        select new object[] { (BeckStyle)style[0], diagram };

    [Theory]
    [MemberData(nameof(StyledDiagrams))]
    public void Style_RendersValidNoNegativePaths(BeckStyle style, string diagram)
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, diagram + ".yaml"));
        var svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = style });

        AssertValidXml(svg);
        AssertNoNegativePathCoords(svg);
    }

    [Theory]
    [MemberData(nameof(StyledDiagrams))]
    public void Style_IsDeterministic(BeckStyle style, string diagram)
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, diagram + ".yaml"));
        var options = new SvgRenderOptions { Style = style };
        var a = BeckSvg.Render(yaml, options);
        var b = BeckSvg.Render(yaml, options);
        Assert.Equal(a, b);
    }

    [Theory]
    [MemberData(nameof(StyledDiagrams))]
    public void Style_DiffersFromClassic(BeckStyle style, string diagram)
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, diagram + ".yaml"));
        var styled = BeckSvg.Render(yaml, new SvgRenderOptions { Style = style });
        var classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.NotEqual(classic, styled);
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Style_LightDarkScrubReducedMotion_AllRender(BeckStyle style)
    {
        var yaml = File.ReadAllText(Path.Combine(_corpusDir, "arch-kitchen.yaml"));

        var variants = new[]
        {
            new SvgRenderOptions { Style = style, Theme = ThemeMode.Light },
            new SvgRenderOptions { Style = style, Theme = ThemeMode.Dark },
            new SvgRenderOptions { Style = style, Animation = AnimationMode.Scrub },
            new SvgRenderOptions { Style = style, Animation = AnimationMode.Static },
        };
        foreach (var options in variants)
        {
            var svg = BeckSvg.Render(yaml, options);
            AssertValidXml(svg);
            AssertNoNegativePathCoords(svg);
        }

        // Reduced motion is a client-side media query, not a render option: confirm all motion CSS
        // is scoped inside the no-preference block, so a reduced-motion viewer gets the static frame.
        var full = BeckSvg.Render(yaml, new SvgRenderOptions { Style = style });
        Assert.Contains("prefers-reduced-motion:no-preference", full);
    }

    // Glow (rebuilt to mock 1g) moves the gradient off edges and onto the node RIM, and paints edges as a
    // faint slate base rail. Two regressions guarded here on a single horizontal Client→API hop — the exact
    // axis-aligned case that used to vanish:
    //   1. The straight base edge paints with a visible SOLID (non-gradient, non-empty) stroke — no
    //      degenerate-bbox gradient can eat an axis-aligned connector, because edges no longer gradient.
    //   2. Every glass node rim references the ONE shared node gradient, which is defined. That gradient
    //      may legitimately use objectBoundingBox units: a node rect always has positive area (unlike the
    //      zero-area straight edge the userSpaceOnUse edge fix addressed), so a single objectBoundingBox
    //      def paints each node's own corner-to-corner cyan→violet rim identically to a per-node
    //      userSpaceOnUse gradient while serving all nodes at once (the mock's single gg-a gradient).
    [Fact]
    public void Glow_StraightEdgePaints_NodesUseGradientRim()
    {
        const string Yaml = """
            type: architecture
            meta: { title: straight, direction: LR }
            nodes:
              - { id: client, title: Client }
              - { id: api, title: API }
            edges:
              - { from: client, to: api }
            """;
        var svg = BeckSvg.Render(Yaml, new SvgRenderOptions { Style = GlowStyle.Instance });

        // (1) Every base edge carries a visible, non-gradient solid stroke — the connector is present.
        // Match only base edges (their inline style starts with "stroke:"); the comet overlay's style
        // starts with "fill:none;" so it is excluded here.
        var edgeStroke = new Regex("<path class=\"beck-edge beck-edge--[^\"]*\"[^>]*style=\"stroke:([^\"]*)\"", RegexOptions.Compiled);
        var matches = edgeStroke.Matches(svg);
        Assert.NotEmpty(matches);
        foreach (Match m in matches)
        {
            var stroke = m.Groups[1].Value;
            Assert.False(string.IsNullOrWhiteSpace(stroke), "edge has empty stroke");
            Assert.DoesNotContain("none", stroke);
            Assert.DoesNotContain("url(", stroke);   // edges are solid rails now, never a gradient
        }

        // (2) The node stroke rule points at the shared node gradient, which is defined in <defs>.
        Assert.Contains("<linearGradient id=\"beck-node-grad-", svg);
        var rimGrad = Regex.Match(svg, "\\.beck-node\\{[^}]*stroke:url\\(#(beck-node-grad-[^)]+)\\)");
        Assert.True(rimGrad.Success, "glow's .beck-node rule should stroke with the node gradient");
        Assert.Contains($"<linearGradient id=\"{rimGrad.Groups[1].Value}\"", svg);
    }

    // Only the router's *edge* paths live on the always-positive canvas — icon glyphs (feather-style
    // paths in their own local 24x24 box) legitimately use negative coordinates, so this scopes to
    // <path class="beck-edge ..."> only, matching CLAUDE.md's "off-canvas edge routes" signature.
    private static readonly Regex _edgePathD = new("<path class=\"beck-edge[^\"]*\"[^>]*\\bd=\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex _numTok = new("-?[0-9]*\\.?[0-9]+", RegexOptions.Compiled);

    private static void AssertNoNegativePathCoords(string svg)
    {
        foreach (Match pm in _edgePathD.Matches(svg))
        {
            foreach (Match nm in _numTok.Matches(pm.Groups[1].Value))
            {
                var v = double.Parse(nm.Value, System.Globalization.CultureInfo.InvariantCulture);
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