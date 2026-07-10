using System.Text.RegularExpressions;
using Beck.Layout;
using Beck.Model;
using Beck.Rendering;
using Beck.Rendering.Text;
using Beck.Styles;
using Beck.Text;
using Xunit;

namespace Beck.Tests;

/// <summary>
/// Phase-4 measurement seam: a style's <see cref="FontRoleTable"/> feeds <see cref="CardSizer"/>
/// (and every render-side measurement) instead of the static <see cref="FontRoles.Of"/>, so a role
/// remap (heavier weight / uppercase / other family) sizes a matching box rather than a classic one
/// the <c>textLength</c> guard would squeeze the real run into. Classic stays byte-identical because
/// its table resolves to the same specs.
/// </summary>
public sealed class StylePhase4Tests
{
    private static NodeModel CardNode(string title) =>
        Validate.LoadDiagram($"type: architecture\nnodes: [{{ id: a, title: \"{title}\" }}]\nedges: []\n").Nodes[0];

    // Classic, but with the card title remapped to weight 800 + uppercase (the brutalist move).
    private static FontRoleTable HeavyUpperTitle() => new(role => role switch
    {
        FontRole.CardTitle => FontRoles.Of(role) with { Weight = 800, Uppercase = true },
        _ => FontRoles.Of(role),
    });

    [Fact]
    public void RemappedTitleRole_MeasuresWiderCard_ThanClassic()
    {
        NodeModel node = CardNode("Payment Gateway");
        StyleGeometry geo = BeckStyle.Classic.Geometry;
        ITextMeasurer m = EmbeddedMetricsMeasurer.For(MetricsFont.Archivo);

        Size classic = CardSizer.Measure(node, m, geo, BeckStyle.Classic.Typography.Roles);
        Size heavy = CardSizer.Measure(node, m, geo, HeavyUpperTitle());

        // Heavier weight + uppercased glyphs both widen the measured title, so the auto-grown card box
        // must be strictly wider than the classic-role box (same geometry, same measurer).
        Assert.True(heavy.W > classic.W, $"expected heavy uppercase title to widen the card: classic={classic.W} heavy={heavy.W}");
    }

    [Fact]
    public void ClassicRoles_MeasureIdenticallyToDefault()
    {
        // Passing the classic table (or nothing) must reproduce the exact classic measurement — the
        // byte-identity anchor at the CardSizer level.
        NodeModel node = CardNode("Payment Gateway Service");
        StyleGeometry geo = BeckStyle.Classic.Geometry;
        ITextMeasurer m = InterMetricsMeasurer.Instance;

        Assert.Equal(CardSizer.Measure(node, m, geo), CardSizer.Measure(node, m, geo, BeckStyle.Classic.Typography.Roles));
    }

    [Fact]
    public void UppercaseSpec_WidensMeasuredWidth_OverLowercase()
    {
        // The seam's core primitive: measuring an uppercase spec measures the transformed string.
        var measurer = EmbeddedMetricsMeasurer.For(MetricsFont.Inter);
        FontRoleSpec lower = FontRoles.Of(FontRole.CardTitle);
        FontRoleSpec upper = lower with { Uppercase = true };

        double lo = measurer.Measure("gateway", FontRole.CardTitle, lower).Width;
        double hi = measurer.Measure("gateway", FontRole.CardTitle, upper).Width;
        Assert.True(hi > lo, $"uppercase must widen: lower={lo} upper={hi}");
    }

    [Fact]
    public void HeavierWeightSpec_WidensMeasuredWidth()
    {
        // Archivo covers weight 800; a heavier spec must select a wider advance row.
        var measurer = EmbeddedMetricsMeasurer.For(MetricsFont.Archivo);
        FontRoleSpec w600 = FontRoles.Of(FontRole.CardTitle); // weight 600
        FontRoleSpec w800 = w600 with { Weight = 800 };

        double a = measurer.Measure("Storefront", FontRole.CardTitle, w600).Width;
        double b = measurer.Measure("Storefront", FontRole.CardTitle, w800).Width;
        Assert.True(b > a, $"weight 800 must be wider than 600: 600={a} 800={b}");
    }

    [Fact]
    public void SpecOverload_DefaultInterfaceMethod_FallsBackToRoleForNonOverridingMeasurer()
    {
        // A measurer that implements only the two-arg role method gets the default interface impl for
        // the three-arg overload, which delegates to the role method (source-compatible).
        ITextMeasurer stub = new RoleOnlyMeasurer();
        FontRoleSpec ignored = FontRoles.Of(FontRole.CardTitle) with { Weight = 900, Uppercase = true };
        Assert.Equal(stub.Measure("abc", FontRole.CardTitle), stub.Measure("abc", FontRole.CardTitle, ignored));
    }

    private sealed class RoleOnlyMeasurer : ITextMeasurer
    {
        public TextMetrics Measure(string text, FontRole role) => new(text.Length * 7, 10, 3);
    }

    // ---- Brutalist end-to-end (the shipped style that exercises the seam) ----

    [Fact]
    public void Brutalist_HeavyUppercaseTitle_ReachesRenderedMarkup()
    {
        // The rendered card title carries font-weight 800 and the uppercased string, matching the
        // measured box — end-to-end proof the remapped role feeds both measurement and rendering.
        string yaml = "type: architecture\nnodes: [{ id: a, title: Storefront }]\nedges: []\n";
        string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BrutalistStyle.Instance });
        Assert.Contains("font-weight=\"800\"", svg);
        Assert.Contains(">STOREFRONT<", svg);
    }

    [Fact]
    public void Brutalist_DiffersFromClassic_AndIsDeterministic()
    {
        string yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Corpus", "arch-kitchen.yaml"));
        var opt = new SvgRenderOptions { Style = BrutalistStyle.Instance };
        string a = BeckSvg.Render(yaml, opt);
        string b = BeckSvg.Render(yaml, opt);
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.Equal(a, b);
        Assert.NotEqual(classic, a);
    }

    // ---- Brutalist artwork seam (StyleArtwork.Brutalist offset shadow) ----

    // A shadow rect immediately followed by its node rect, capturing both origins. Requiring the
    // token-driven fill in the pattern proves colour goes through --beck-shadow (no resolved literal),
    // and requiring the trailing <rect class="beck-node…"> proves the shadow sits *behind* the node.
    private static readonly Regex ShadowThenNode = new(
        "<rect class=\"beck-shadow\" x=\"([0-9.]+)\" y=\"([0-9.]+)\" width=\"[0-9.]+\" height=\"[0-9.]+\" rx=\"[0-9.]+\" style=\"fill:var\\(--beck-shadow[^\"]*\\)\"/><rect class=\"beck-node",
        RegexOptions.Compiled);

    [Fact]
    public void Brutalist_DrawsSolidOffsetShadowBehindEachNode_NotOnGroups()
    {
        string yaml = ArchKitchen();
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        string brut = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BrutalistStyle.Instance });

        // Classic (Plain artwork) never emits a shadow rect — the seam is byte-identical off.
        Assert.DoesNotContain("<rect class=\"beck-shadow\"", classic);

        var paired = ShadowThenNode.Matches(brut);
        Assert.True(paired.Count > 0, "brutalist should draw an offset shadow rect behind each node");

        // Every shadow rect in the doc is one of the paired (shadow→node) matches: no shadow leaks
        // onto a group box or ghost/pseudo-state (those pass shadow:false through Artwork.Rect).
        int totalShadowRects = Regex.Matches(brut, "<rect class=\"beck-shadow\"").Count;
        Assert.Equal(totalShadowRects, paired.Count);

        // The shadow is offset from its node by exactly StyleGeometry.ShadowOffset on both axes.
        double off = BrutalistStyle.Instance.Geometry.ShadowOffset;
        Assert.True(off > 0);
        foreach (Match m in paired)
        {
            // shadow origin captured; re-read the node origin from the same span.
            double sx = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            double sy = double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            Match node = Regex.Match(brut.Substring(m.Index + m.Length - "<rect class=\"beck-node".Length),
                "^<rect class=\"beck-node[^\"]*\" x=\"([0-9.]+)\" y=\"([0-9.]+)\"");
            Assert.True(node.Success);
            double nx = double.Parse(node.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            double ny = double.Parse(node.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(off, sx - nx, 3);
            Assert.Equal(off, sy - ny, 3);
        }
    }

    [Fact]
    public void Brutalist_PacketFlowUsesSteppedEasing_ClassicDoesNot()
    {
        // The stepped-flow seam (StyleMotion.PacketSteps): the packet's offset-distance track carries a
        // steps(n) timing function. arch-kitchen has an authored flow, so packets exist. Classic keeps
        // its smooth per-edge-kind ease (a linear()/none function), never steps(6).
        string yaml = ArchKitchen();
        string brut = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BrutalistStyle.Instance });
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });

        int n = BrutalistStyle.Instance.Motion.PacketSteps!.Value;
        Assert.Contains($"animation-timing-function:steps({n})", brut);
        Assert.DoesNotContain($"steps({n})", classic);
    }

    // ---- Brutalist edge presentation (mock 1d): a stepped lime/yellow pulse ticks every connector ----

    // Brutalist's headline edge motion is a Comet overlay on EVERY architecture edge, ticking in 8 hard
    // discrete steps (the mock's `stroke-dasharray:6 297;animation:ptd 1.8s steps(8) infinite`): each
    // overlay path shares its base edge's exact d, the palette alternates the two lime/yellow signal hues,
    // and the loop is compiled onto the shared cycle with no delay chain.
    [Fact]
    public void Brutalist_SteppedPulseOnEveryEdge_SharesD_NoDelayChain()
    {
        string svg = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = BrutalistStyle.Instance });

        var baseD = new Regex("<path class=\"beck-edge beck-edge--[^\"]*\" d=\"([^\"]*)\"")
            .Matches(svg).Select(m => m.Groups[1].Value).ToList();
        var overlayD = new Regex("<path class=\"beck-edge-overlay [^\"]*\" d=\"([^\"]*)\"")
            .Matches(svg).Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(overlayD);
        Assert.Equal(baseD.Count, overlayD.Count);
        foreach (string od in overlayD) Assert.Contains(od, baseD);

        // Stepped tick (steps(8)) on a compiled 1.8s shared-cycle loop, guarded, no delay chain.
        Assert.Matches(@"\.beo0-[0-9a-z]+\{animation:kbeo0-[0-9a-z]+ 1\.8s steps\(8\) infinite;\}", svg);
        Assert.Contains("@keyframes kbeo0-", svg);
        Assert.DoesNotContain("animation-delay", svg);

        // The pulse alternates the two brutalist signal hues; the block is a squared (butt) 6px dash.
        Assert.Contains("stroke:var(--beck-pulse-1);stroke-width:6;stroke-linecap:butt;", svg);
        Assert.Contains("stroke:var(--beck-pulse-2);stroke-width:6;stroke-linecap:butt;", svg);
    }

    // Brutalist's thick edge needs the sane marker sizing (userSpaceOnUse, sub-linear growth) AND the
    // mock's lime-fill / connector-outlined arrowhead — a filled polygon coloured through --beck-pulse-1
    // with a --beck-edge outline stroke, drawn overflow-visible so the outline isn't clipped.
    [Fact]
    public void Brutalist_Arrowhead_IsScaledLimeFilled_WithContrastOutline()
    {
        string svg = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = BrutalistStyle.Instance });

        Assert.Contains("markerUnits=\"userSpaceOnUse\"", svg);
        Assert.Contains("overflow=\"visible\"", svg);
        Assert.Contains("<polygon points=\"0,1 10,5 0,9\" fill=\"var(--beck-pulse-1)\" stroke=\"var(--beck-edge)\" stroke-width=\"1.5\" stroke-linejoin=\"round\"/>", svg);
    }

    // ---- Sketch artwork seam (StyleArtwork.Sketch) ----

    private static string ArchKitchen() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Corpus", "arch-kitchen.yaml"));

    [Fact]
    public void Sketch_NodeCardsBecomeWobblyPaths_NotRects()
    {
        // The artwork seam: under StyleArtwork.Sketch every node card that classic draws as
        // <rect class="beck-node…"> becomes a <path class="beck-node…"> carrying the same class (so the
        // token-driven fill/stroke/filter still apply). Classic keeps rects. The count of node paths
        // equals the count of node rects classic drew, so no node was dropped.
        string yaml = ArchKitchen();
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        string sketch = BeckSvg.Render(yaml, new SvgRenderOptions { Style = SketchStyle.Instance });

        int classicRects = Regex.Matches(classic, "<rect class=\"beck-node[ \"]").Count;
        int sketchPaths = Regex.Matches(sketch, "<path class=\"beck-node[ \"]").Count;
        Assert.True(classicRects > 0, "corpus should draw node rects under classic");
        Assert.Equal(classicRects, sketchPaths);
        // Sketch draws no node card as a straight rect (only the class-card clipPath rect, which has no
        // beck-node class, remains a rect).
        Assert.DoesNotContain("<rect class=\"beck-node ", sketch);
        Assert.DoesNotContain("<rect class=\"beck-node\"", sketch);
    }

    [Fact]
    public void Sketch_WobblePathsAreClosed_AndVaryPerNode()
    {
        // Each wobbly outline is one continuous closed path (starts M, ends Z) and different nodes get
        // different jitter — proving the seed is keyed off the node id, not a single global constant.
        string sketch = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = SketchStyle.Instance });
        var ds = Regex.Matches(sketch, "<path class=\"beck-node[^\"]*\" d=\"([^\"]*)\"")
                      .Select(mm => mm.Groups[1].Value).ToList();
        Assert.True(ds.Count >= 2, "need at least two node cards to compare wobble");
        foreach (string d in ds)
        {
            Assert.StartsWith("M", d);
            Assert.EndsWith("Z", d);
        }
        Assert.True(ds.Distinct().Count() > 1, "different nodes must wobble differently (per-node seed)");
    }

    [Fact]
    public void Sketch_StartEndPseudoStates_BecomeWobblyBlobs()
    {
        // State diagrams: the start/end pseudo-state circles become wobbly closed paths too, so the
        // hand-drawn identity is consistent across diagram types.
        string yaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Corpus", "state.yaml"));
        string sketch = BeckSvg.Render(yaml, new SvgRenderOptions { Style = SketchStyle.Instance });
        Assert.Contains("<path class=\"beck-node--start\"", sketch);
        Assert.DoesNotContain("<circle class=\"beck-node--start\"", sketch);
        Assert.DoesNotContain("<circle class=\"beck-node--end\"", sketch);
    }

    [Fact]
    public void Sketch_GroupBoxesWobble_AndKeepPerAccentStroke()
    {
        // Group boxes wobble as well, and the per-group accent stroke (an inline style attr) survives the
        // rect→path swap.
        string sketch = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = SketchStyle.Instance });
        Assert.Contains("<path class=\"beck-group\"", sketch);
        Assert.Matches("<path class=\"beck-group\" d=\"[^\"]*\" style=\"stroke:color-mix", sketch);
    }

    // ---- Extrude artwork seam (StyleArtwork.Extruded depth faces + press-down) ----

    // Two depth faces (bottom then right) drawn immediately before their node rect: both filled
    // through --beck-depth-* tokens (no resolved literal), and the trailing <rect class="beck-node…">
    // proves they sit *behind* the node.
    private static readonly Regex DepthThenNode = new(
        "<path class=\"beck-depth beck-depth--bottom\" d=\"([^\"]*)\" style=\"fill:var\\(--beck-depth-bottom[^\"]*\\)\"/>" +
        "<path class=\"beck-depth beck-depth--right\" d=\"[^\"]*\" style=\"fill:var\\(--beck-depth-right[^\"]*\\)\"/>" +
        "<rect class=\"beck-node",
        RegexOptions.Compiled);

    [Fact]
    public void Extrude_DrawsTwoDepthFacesBehindEachNode_NotOnGroupsOrGhosts()
    {
        string yaml = ArchKitchen();
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        string ext = BeckSvg.Render(yaml, new SvgRenderOptions { Style = ExtrudeStyle.Instance });

        // Classic (Plain artwork) never emits a depth face — the seam is byte-identical off.
        Assert.DoesNotContain("beck-depth", classic);

        var paired = DepthThenNode.Matches(ext);
        Assert.True(paired.Count > 0, "extrude should draw depth faces behind each card node");

        // Every depth face in the doc is one of the paired (face→node) matches — a right face for every
        // bottom face, and none leaking onto a group box or ghost (those pass shadow:false).
        int bottoms = Regex.Matches(ext, "beck-depth--bottom").Count;
        int rights = Regex.Matches(ext, "beck-depth--right").Count;
        Assert.Equal(bottoms, rights);
        Assert.Equal(bottoms, paired.Count);
    }

    [Fact]
    public void Extrude_DepthFaceOffsetMatchesDepthOffset()
    {
        // The bottom face's back edge is offset from its front edge by exactly StyleGeometry.DepthOffset
        // on both axes: the face path is M x yb L xr yb L xr+d yb+d L x+d yb+d Z, so the last two points
        // are the first two shifted by (d,d).
        double d = ExtrudeStyle.Instance.Geometry.DepthOffset;
        Assert.True(d > 0);

        string ext = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = ExtrudeStyle.Instance });
        Match m = DepthThenNode.Match(ext);
        Assert.True(m.Success);

        // Parse the bottom-face d: four "x y" points.
        var nums = Regex.Matches(m.Groups[1].Value, "-?[0-9]*\\.?[0-9]+")
                        .Select(mm => double.Parse(mm.Value, System.Globalization.CultureInfo.InvariantCulture))
                        .ToArray();
        Assert.Equal(8, nums.Length); // 4 points × 2
        // p0=(x,yb) p1=(xr,yb) p2=(xr+d,yb+d) p3=(x+d,yb+d)
        Assert.Equal(d, nums[4] - nums[2], 3); // p2.x - p1.x == d
        Assert.Equal(d, nums[5] - nums[3], 3); // p2.y - p1.y == d
        Assert.Equal(d, nums[6] - nums[0], 3); // p3.x - p0.x == d
        Assert.Equal(d, nums[7] - nums[1], 3); // p3.y - p0.y == d
    }

    [Fact]
    public void Extrude_ActiveEffectPressesDown_ClassicLifts()
    {
        // The press-down seam (StyleMotion.PressDown): the pulse/highlight transform keyframe presses
        // the node toward its faces (translate(2px,2px)) instead of classic's translateY(-2px) lift.
        // arch-kitchen has an authored flow with card effects, so a transform track exists.
        string yaml = ArchKitchen();
        string ext = BeckSvg.Render(yaml, new SvgRenderOptions { Style = ExtrudeStyle.Instance });
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });

        Assert.Contains("transform:translate(2px,2px)", ext);
        Assert.DoesNotContain("translateY(-2px)", ext);

        Assert.Contains("translateY(-2px)", classic);
        Assert.DoesNotContain("transform:translate(2px,2px)", classic);
    }

    [Fact]
    public void Extrude_DiffersFromClassic_AndIsDeterministic()
    {
        string yaml = ArchKitchen();
        var opt = new SvgRenderOptions { Style = ExtrudeStyle.Instance };
        string a = BeckSvg.Render(yaml, opt);
        string b = BeckSvg.Render(yaml, opt);
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.Equal(a, b);
        Assert.NotEqual(classic, a);
    }

    // ---- Circuit artwork seam (StyleArtwork.Circuit chip pins + trace vias) ----

    // A run of two-or-more chip pin rects (left+right per row) immediately followed by the node rect
    // they sit behind — proving pins draw *before* (behind) the node and go through the --beck-pin token
    // (no resolved literal). Group/ghost/pseudo-state chrome passes shadow:false so gets no pins.
    private static readonly Regex PinsThenNode = new(
        "(<rect class=\"beck-pin\" x=\"[0-9.-]+\" y=\"[0-9.-]+\" width=\"[0-9.]+\" height=\"[0-9.]+\" rx=\"[0-9.]+\" style=\"fill:var\\(--beck-pin[^\"]*\\)\"/>){2,}<rect class=\"beck-node",
        RegexOptions.Compiled);

    // Matches only the base flow edge path (`beck-edge beck-edge--…`), NOT the static trace-bed underlay
    // (`beck-edge-bed`), which shares the same `d`; counting the bed too would double every edge's bends.
    private static readonly Regex EdgePathDLocal = new("<path class=\"beck-edge beck-edge--[^\"]*\"[^>]*\\bd=\"([^\"]*)\"", RegexOptions.Compiled);

    [Fact]
    public void Circuit_ViaDotCountMatchesEdgeBendCount()
    {
        // The requested invariant: circuit drops exactly one via dot at each genuine route bend. A
        // step-round edge path renders each real corner as a `Q` command (collinear points stay `L`), so
        // the number of <circle class="beck-via"> must equal the total `Q` count across every edge path.
        string yaml = ArchKitchen();
        string circuit = BeckSvg.Render(yaml, new SvgRenderOptions { Style = CircuitStyle.Instance });
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });

        int vias = Regex.Matches(circuit, "<circle class=\"beck-via\"").Count;
        int bends = EdgePathDLocal.Matches(circuit).Sum(m => m.Groups[1].Value.Count(ch => ch == 'Q'));
        Assert.True(bends > 0, "arch-kitchen should route at least one bent edge");
        Assert.Equal(bends, vias);

        // Classic (Plain artwork) emits no vias — the seam is byte-identical off.
        Assert.DoesNotContain("beck-via", classic);
    }

    [Fact]
    public void Circuit_ViaDotsUseTokenColour_AndSitOnTheTrace()
    {
        // Every via is filled through --beck-via (no resolved literal) and sits at a coordinate that
        // appears verbatim in some edge path's `d` (i.e. on a real trace vertex, not a made-up point).
        string circuit = BeckSvg.Render(ArchKitchen(), new SvgRenderOptions { Style = CircuitStyle.Instance });
        var vias = Regex.Matches(circuit, "<circle class=\"beck-via\" cx=\"([0-9.-]+)\" cy=\"([0-9.-]+)\" r=\"[0-9.]+\" style=\"fill:var\\(--beck-via[^\"]*\\)\"/>");
        Assert.True(vias.Count > 0);
        string allEdgeD = string.Concat(EdgePathDLocal.Matches(circuit).Select(m => m.Groups[1].Value));
        foreach (Match v in vias)
        {
            // via coords are non-negative (on the always-positive canvas) and land on an edge vertex.
            Assert.True(double.Parse(v.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= 0);
            Assert.Contains($"{v.Groups[1].Value} {v.Groups[2].Value}", allEdgeD);
        }
    }

    [Fact]
    public void Circuit_DrawsChipPinsBehindNodes_NotOnGroups()
    {
        string yaml = ArchKitchen();
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        string circuit = BeckSvg.Render(yaml, new SvgRenderOptions { Style = CircuitStyle.Instance });

        // Classic (Plain artwork) never emits a chip pin — byte-identical off.
        Assert.DoesNotContain("beck-pin", classic);

        // Pins come in even counts (a left + right pin per row) and precede the node rect they back.
        int pins = Regex.Matches(circuit, "<rect class=\"beck-pin\"").Count;
        Assert.True(pins > 0, "circuit should draw chip pins on card/pill/class nodes");
        Assert.Equal(0, pins % 2);
        Assert.True(PinsThenNode.Matches(circuit).Count > 0, "each pin ladder should sit behind a node rect");

        // No pins leak onto the group boxes: the beck-groups layer contains no pin rects.
        Match groups = Regex.Match(circuit, "<g class=\"beck-groups\">(.*?)</g>", RegexOptions.Singleline);
        if (groups.Success) Assert.DoesNotContain("beck-pin", groups.Groups[1].Value);
    }

    [Fact]
    public void Circuit_DiffersFromClassic_AndIsDeterministic()
    {
        string yaml = ArchKitchen();
        var opt = new SvgRenderOptions { Style = CircuitStyle.Instance };
        string a = BeckSvg.Render(yaml, opt);
        string b = BeckSvg.Render(yaml, opt);
        string classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.Equal(a, b);
        Assert.NotEqual(classic, a);
    }

    [Fact]
    public void Plain_StylesStillEmitRects_SeamDefaultIsPlain()
    {
        // The seam defaults to StyleArtwork.Plain: every non-sketch style (classic included) keeps
        // straight node rects — the wobble is opt-in per style, so nothing else regressed.
        string yaml = ArchKitchen();
        foreach (BeckStyle style in new[] { BeckStyle.Classic, MinimalStyle.Instance, BrutalistStyle.Instance })
        {
            string svg = BeckSvg.Render(yaml, new SvgRenderOptions { Style = style });
            Assert.Contains("<rect class=\"beck-node", svg);
            Assert.DoesNotContain("<path class=\"beck-node ", svg);
        }
    }
}
