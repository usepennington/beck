using System.Globalization;
using System.Text.RegularExpressions;
using Beck.Rendering;
using Beck.Rendering.Svg;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Targeted assertions for the per-style <em>edge-presentation</em> seam (<see cref="StyleEdges"/>):
/// the base-layer treatment, the optional overlay layer sharing the edge's exact <c>d</c>, the
/// arrowhead presentation + marker scaling, the deterministic path bow, and the lifeline/separator
/// treatment. Every classic default is byte-inert (proven wholesale by
/// <see cref="StyleByteIdentityTests"/> + the 128-file byte diff); these pin the <em>opted-in</em>
/// behaviour and its invariants (overlay shares <c>d</c>, bow preserves endpoints + stays one path,
/// OpenV emits two strokes, markers scale, no delay chains).
/// </summary>
public sealed class EdgePresentationTests
{
    private static readonly string CorpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static string Yaml(string f) => File.ReadAllText(Path.Combine(CorpusDir, f));

    private static BeckStyle WithEdges(Func<StyleEdges, StyleEdges> f) =>
        BeckStyle.Classic with { Edges = f(StyleEdges.Classic) };

    private static readonly Regex BaseEdgeD = new("<path class=\"beck-edge beck-edge--[^\"]*\" d=\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex OverlayD = new("<path class=\"beck-edge-overlay [^\"]*\" d=\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex MarkerW = new("markerWidth=\"([^\"]*)\"", RegexOptions.Compiled);

    private static List<string> Matches(Regex r, string s) => r.Matches(s).Select(m => m.Groups[1].Value).ToList();
    private static List<double> Nums(string d) =>
        Regex.Matches(d, "-?\\d+(?:\\.\\d+)?").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
    private static (double X, double Y) First(string d) { var n = Nums(d); return (n[0], n[1]); }
    private static (double X, double Y) Last(string d) { var n = Nums(d); return (n[^2], n[^1]); }

    // ---- overlay layer ----

    // A Comet overlay drops one additional path PER edge whose d is byte-identical to the edge's own d
    // (a decoration sharing the geometry, never a split of the single continuous edge path).
    [Fact]
    public void Overlay_SharesEdgeD_OnePerEdge()
    {
        string svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.Comet }) });

        var baseD = Matches(BaseEdgeD, svg);
        var overlayD = Matches(OverlayD, svg);
        Assert.NotEmpty(overlayD);
        Assert.Equal(baseD.Count, overlayD.Count);
        foreach (string od in overlayD) Assert.Contains(od, baseD);
    }

    // The overlay compiles to a self-contained shared-cycle loop (@keyframes kbeo…) under the
    // reduced-motion guard, with NO animation-delay chain anywhere.
    [Fact]
    public void Overlay_Compiled_NoDelayChain()
    {
        string svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.Comet }) });

        Assert.Contains("@keyframes kbeo0-", svg);
        Assert.Contains("@media (prefers-reduced-motion:no-preference)", svg);
        Assert.DoesNotContain("animation-delay", svg);
    }

    // Classic (Overlay=None) emits neither overlay markup nor its keyframes — the seam is byte-inert off.
    [Fact]
    public void Classic_EmitsNoOverlay()
    {
        string svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"), new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.DoesNotContain("beck-edge-overlay", svg);
        Assert.DoesNotContain("kbeo", svg);
    }

    // A sequence message also carries its overlay, sharing the message path's d.
    [Fact]
    public void Overlay_OnSequenceMessages()
    {
        string svg = BeckSvg.Render(Yaml("sample-sequence.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.Comet }) });
        var baseD = Matches(BaseEdgeD, svg);
        var overlayD = Matches(OverlayD, svg);
        Assert.NotEmpty(overlayD);
        foreach (string od in overlayD) Assert.Contains(od, baseD);
    }

    // ---- path bow ----

    // Bowing changes the drawn geometry but keeps every edge's two endpoints exactly, and the edge is
    // still ONE continuous path (a single M). Compared edge-for-edge against the classic (unbowed) render.
    [Fact]
    public void Bow_PreservesEndpoints_OnePath()
    {
        string yaml = Yaml("arch-kitchen.yaml");
        var classic = Matches(BaseEdgeD, BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));
        var bowed = Matches(BaseEdgeD, BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { BowAmplitude = 6 }) }));

        Assert.Equal(classic.Count, bowed.Count);
        Assert.NotEmpty(bowed);
        bool anyChanged = false;
        for (int i = 0; i < classic.Count; i++)
        {
            Assert.Equal(First(classic[i]).X, First(bowed[i]).X, 2);
            Assert.Equal(First(classic[i]).Y, First(bowed[i]).Y, 2);
            Assert.Equal(Last(classic[i]).X, Last(bowed[i]).X, 2);
            Assert.Equal(Last(classic[i]).Y, Last(bowed[i]).Y, 2);
            Assert.Equal(1, bowed[i].Count(ch => ch == 'M'));       // still one continuous path
            if (classic[i] != bowed[i]) anyChanged = true;
        }
        Assert.True(anyChanged, "bow should perturb at least one edge's geometry");
    }

    // The bow primitive is deterministic and preserves the two endpoints of a straight run, replacing it
    // with a quadratic (Q) through a displaced midpoint.
    [Fact]
    public void BowLine_PreservesEndpoints_IsCurvedAndDeterministic()
    {
        string a = Shaping.BowLine(104, 53, 126, 53, 5, "seed-1");
        string b = Shaping.BowLine(104, 53, 126, 53, 5, "seed-1");
        Assert.Equal(a, b);                                        // deterministic for a fixed seed
        Assert.Equal((104d, 53d), First(a));
        Assert.Equal((126d, 53d), Last(a));
        Assert.Equal(1, a.Count(ch => ch == 'M'));
        Assert.Contains("Q", a);
        Assert.NotEqual(a, Shaping.BowLine(104, 53, 126, 53, 5, "seed-2"));  // seed steers the wobble
    }

    // ---- arrowhead presentation ----

    // OpenV replaces the filled arrowhead with TWO round-capped strokes running back from the tip; the
    // filled polygon body is gone.
    [Fact]
    public void OpenV_EmitsTwoStrokes()
    {
        string svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Arrow = EdgeArrow.OpenV }) });

        Assert.Contains("<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"1.5\" stroke=", svg);
        Assert.Contains("<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"8.5\" stroke=", svg);
        Assert.DoesNotContain("points=\"0,1 10,5 0,9\"", svg);     // the classic filled arrow is gone
    }

    // Classic keeps the filled polygon arrowhead.
    [Fact]
    public void Classic_FilledArrow()
    {
        string svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"), new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.Contains("points=\"0,1 10,5 0,9\"", svg);
    }

    // ---- marker scaling ----

    // MarkerScale multiplies the marker geometry in the default strokeWidth units (classic arrow = 6 → 12
    // at ×2), with no markerUnits switch.
    [Fact]
    public void MarkerScale_MultipliesGeometry()
    {
        string yaml = Yaml("arch-kitchen.yaml");
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        string scaled = BeckSvg.Render(yaml, new SvgRenderOptions { Style = WithEdges(e => e with { MarkerScale = 2 }) });

        Assert.Contains("markerWidth=\"6\"", classic);
        Assert.Contains("markerWidth=\"12\"", scaled);
        Assert.DoesNotContain("markerUnits", scaled);              // still strokeWidth units
    }

    // MarkerScaleToWidth switches to userSpaceOnUse and grows the marker sub-linearly with the edge
    // stroke width — the "sane scaling" a thick line needs. Classic never emits markerUnits.
    [Fact]
    public void MarkerScaleToWidth_UsesUserSpaceOnUse()
    {
        string yaml = Yaml("arch-kitchen.yaml");
        Assert.DoesNotContain("markerUnits", BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));

        string svg = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { MarkerScaleToWidth = true }) });
        Assert.Contains("markerUnits=\"userSpaceOnUse\"", svg);
        // The arrow's classic base width is 6; scaled to width it grows (>6).
        double w = Matches(MarkerW, svg).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).Max();
        Assert.True(w > 6, $"expected a grown marker width, got {w}");
    }

    // ---- base-layer treatment ----

    [Fact]
    public void BaseOpacity_And_BaseLinecap()
    {
        string yaml = Yaml("arch-kitchen.yaml");
        string faint = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { BaseOpacity = 0.35, BaseLinecap = "butt" }) });
        Assert.Contains("stroke-opacity:0.35", faint);
        Assert.Contains(".beck-edge{fill:none;stroke-width:1.6;stroke-linecap:butt;", faint);

        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.DoesNotContain("stroke-opacity:0.35", classic);    // no base-edge opacity by default
        Assert.Contains(".beck-edge{fill:none;stroke-width:1.6;stroke-linecap:round;", classic);
    }

    // ---- lifelines / separators ----

    [Fact]
    public void Lifeline_FaintSolid_DropsDash()
    {
        string yaml = Yaml("sample-sequence.yaml");
        Assert.Contains("stroke-dasharray:6 7", BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));

        string solid = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { Lifeline = LifelineShape.FaintSolid }) });
        Assert.Contains(".beck-lifeline{stroke-width:2;}", solid);
    }

    [Fact]
    public void Lifeline_Wobbly_SwapsLineForPath()
    {
        string yaml = Yaml("sample-sequence.yaml");
        Assert.Contains("<line class=\"beck-lifeline\"", BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));

        string wobbly = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { Lifeline = LifelineShape.Wobbly }) });
        Assert.Contains("<path class=\"beck-lifeline\"", wobbly);
        Assert.DoesNotContain("<line class=\"beck-lifeline\"", wobbly);
    }

    [Fact]
    public void WobblySeparators_SwapClassLinesForPaths()
    {
        string yaml = Yaml("class.yaml");
        Assert.Contains("<line class=\"beck-class-head-border\"", BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));

        string wobbly = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { WobblySeparators = true }) });
        Assert.Contains("<path class=\"beck-class-head-border\"", wobbly);
        Assert.DoesNotContain("<line class=\"beck-class-head-border\"", wobbly);
    }
}
