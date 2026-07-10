using System.Globalization;
using System.Text.RegularExpressions;
using Beck.Styles;
using Beck.Svg;
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
    private static readonly string _corpusDir = Path.Combine(AppContext.BaseDirectory, "Corpus");
    private static string Yaml(string f) => File.ReadAllText(Path.Combine(_corpusDir, f));

    private static BeckStyle WithEdges(Func<StyleEdges, StyleEdges> f) =>
        BeckStyle.Classic with { Edges = f(StyleEdges.Classic) };

    private static readonly Regex _baseEdgeD = new("<path class=\"beck-edge beck-edge--[^\"]*\" d=\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex _baseEdgeStroke = new("<path class=\"beck-edge beck-edge--[^\"]*\" d=\"[^\"]*\" style=\"stroke:([^;\"]+)", RegexOptions.Compiled);
    private static readonly Regex _stationStroke = new("<circle class=\"beck-station\"[^>]*;stroke:([^;\"]+)", RegexOptions.Compiled);
    private static readonly Regex _overlayD = new("<path class=\"beck-edge-overlay [^\"]*\" d=\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex _bedD = new("<path class=\"beck-edge-bed\" d=\"([^\"]*)\"", RegexOptions.Compiled);
    private static readonly Regex _markerW = new("markerWidth=\"([^\"]*)\"", RegexOptions.Compiled);

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
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.Comet }) });

        var baseD = Matches(_baseEdgeD, svg);
        var overlayD = Matches(_overlayD, svg);
        Assert.NotEmpty(overlayD);
        Assert.Equal(baseD.Count, overlayD.Count);
        foreach (var od in overlayD)
        {
            Assert.Contains(od, baseD);
        }
    }

    // The overlay compiles to a self-contained shared-cycle loop (@keyframes kbeo…) under the
    // reduced-motion guard, with NO animation-delay chain anywhere.
    [Fact]
    public void Overlay_Compiled_NoDelayChain()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.Comet }) });

        Assert.Contains("@keyframes kbeo0-", svg);
        Assert.Contains("@media (prefers-reduced-motion:no-preference)", svg);
        Assert.DoesNotContain("animation-delay", svg);
    }

    // Classic (Overlay=None) emits neither overlay markup nor its keyframes — the seam is byte-inert off.
    [Fact]
    public void Classic_EmitsNoOverlay()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"), new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.DoesNotContain("beck-edge-overlay", svg);
        Assert.DoesNotContain("kbeo", svg);
    }

    // A sequence message also carries its overlay, sharing the message path's d.
    [Fact]
    public void Overlay_OnSequenceMessages()
    {
        var svg = BeckSvg.Render(Yaml("sample-sequence.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.Comet }) });
        var baseD = Matches(_baseEdgeD, svg);
        var overlayD = Matches(_overlayD, svg);
        Assert.NotEmpty(overlayD);
        foreach (var od in overlayD)
        {
            Assert.Contains(od, baseD);
        }
    }

    // OverlaySteps swaps the overlay animation's linear timing for steps(n) — brutalist/terminal's
    // mechanical tick — in the emitted overlay class rule, while the loop stays compiled + infinite with
    // no delay chain. Unset (classic) stays linear.
    [Fact]
    public void OverlaySteps_EmitsSteppedTiming()
    {
        var yaml = Yaml("arch-kitchen.yaml");

        var linear = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.Comet }) });
        Assert.Matches(@"\.beo0-[0-9a-f]+\{animation:kbeo0-[0-9a-f]+ [0-9.]+s linear infinite;\}", linear);
        Assert.DoesNotContain("steps(", linear);

        var stepped = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.Comet, OverlaySteps = 8 }) });
        Assert.Matches(@"\.beo0-[0-9a-f]+\{animation:kbeo0-[0-9a-f]+ [0-9.]+s steps\(8\) infinite;\}", stepped);
        // Still a compiled shared-cycle loop, still guarded, still no delay chain.
        Assert.Contains("@keyframes kbeo0-", stepped);
        Assert.DoesNotContain("animation-delay", stepped);
    }

    // OverlaySteps also steps a Marching overlay (blueprint-style dashed flow), but is ignored for a
    // DrawOn wipe — that eased ink stays linear even when steps are set.
    [Fact]
    public void OverlaySteps_AppliesToMarching_NotDrawOn()
    {
        var yaml = Yaml("arch-kitchen.yaml");

        var marching = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.Marching, OverlaySteps = 12 }) });
        Assert.Contains("steps(12)", marching);

        var drawOn = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.DrawOn, OverlaySteps = 12 }) });
        Assert.DoesNotContain("steps(", drawOn);
    }

    // ---- base-colour palette (metro's per-line transit hues) ----

    // A 4-node LR chain: three plain Data edges (a->b, b->c, c->d), all on the DEFAULT edge colour
    // (var(--beck-edge)). Routed-edge index == model.Edges order == SVG document order, so a positional
    // read of the base strokes is exactly the palette cycle.
    private const string PaletteChainYaml = """
        type: architecture
        meta: { title: t, direction: LR }
        nodes:
          - { id: a, title: A }
          - { id: b, title: B }
          - { id: c, title: C }
          - { id: d, title: D }
        edges:
          - { from: a, to: b }
          - { from: b, to: c }
          - { from: c, to: d }
        """;

    // BaseColorPalette recolours each default-coloured base edge stroke by its stable draw-order index,
    // cycling palette[i % Count] — so a 2-hue palette over 3 edges reads hue0, hue1, hue0.
    [Fact]
    public void BaseColorPalette_CyclesBaseStrokeByEdgeIndex()
    {
        var svg = BeckSvg.Render(PaletteChainYaml,
            new SvgRenderOptions
            {
                Style = WithEdges(e => e with { BaseColorPalette = ["var(--pl-a)", "var(--pl-b)"] }),
            });

        var strokes = Matches(_baseEdgeStroke, svg);
        Assert.Equal(new[] { "var(--pl-a)", "var(--pl-b)", "var(--pl-a)" }, strokes);
    }

    // An edge with an explicit author colour keeps it — the palette only paints edges still on the default
    // var(--beck-edge). The palette index still ADVANCES across the authored edge (the third edge takes
    // palette[2], not palette[1]), so a mixed line stays coherently indexed.
    [Fact]
    public void BaseColorPalette_AuthoredEdgeColorWins()
    {
        const string Yaml = """
            type: architecture
            meta: { title: t, direction: LR }
            nodes:
              - { id: a, title: A }
              - { id: b, title: B }
              - { id: c, title: C }
              - { id: d, title: D }
            edges:
              - { from: a, to: b }
              - { from: b, to: c, color: danger }
              - { from: c, to: d }
            """;
        var svg = BeckSvg.Render(Yaml,
            new SvgRenderOptions
            {
                Style = WithEdges(e => e with { BaseColorPalette = ["var(--pl-a)", "var(--pl-b)", "var(--pl-c)"] }),
            });

        var strokes = Matches(_baseEdgeStroke, svg);
        Assert.Equal(new[] { "var(--pl-a)", "var(--beck-danger)", "var(--pl-c)" }, strokes);
    }

    // Empty palette (classic, every non-metro style) leaves every base edge on the single var(--beck-edge)
    // token — the seam is byte-inert off (the wholesale proof is the 128-file byte diff + byte-identity tests).
    [Fact]
    public void BaseColorPalette_Empty_LeavesDefaultEdgeColour()
    {
        var svg = BeckSvg.Render(PaletteChainYaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        var strokes = Matches(_baseEdgeStroke, svg);
        Assert.Equal(new[] { "var(--beck-edge)", "var(--beck-edge)", "var(--beck-edge)" }, strokes);
    }

    // Coherence: the station-dot RINGS (the StyleArtwork.Metro transit seam, available to custom
    // styles) follow the same per-line palette as the base stroke, so a line and its two stations
    // read one hue. On the default-coloured chain each edge i takes palette[i%3]; its two stations
    // take the SAME colour (stations are emitted per edge, in edge order, two apiece).
    [Fact]
    public void BaseColorPalette_StationRingsFollowLineHue()
    {
        var transit = BeckStyle.Classic with
        {
            Name = "custom-transit",
            Artwork = StyleArtwork.Metro,
            Geometry = BeckStyle.Classic.Geometry with { StationRadius = 4.5 },
            Edges = StyleEdges.Classic with { BaseColorPalette = ["var(--pl-a)", "var(--pl-b)", "var(--pl-c)"] },
        };
        var svg = BeckSvg.Render(PaletteChainYaml, new SvgRenderOptions { Style = transit });

        var strokes = Matches(_baseEdgeStroke, svg);
        Assert.Equal(new[] { "var(--pl-a)", "var(--pl-b)", "var(--pl-c)" }, strokes);

        var stations = Matches(_stationStroke, svg);
        Assert.Equal(new[]
        {
            "var(--pl-a)", "var(--pl-a)",
            "var(--pl-b)", "var(--pl-b)",
            "var(--pl-c)", "var(--pl-c)",
        }, stations);
    }

    // A sequence message departs in the colour of its SOURCE participant's line: when the source
    // carries an explicit accent, the message (like the source's lifeline) keeps that accent — not
    // the destination/worker accent the model folded into edge.Color, and not a palette hue.
    [Fact]
    public void BaseColorPalette_SequenceMessageFollowsExplicitSourceAccent()
    {
        const string Yaml = """
            type: sequence
            participants:
              - { id: a, title: A, accent: danger }
              - { id: b, title: B }
            messages:
              - { from: a, to: b, label: go }
            """;
        var svg = BeckSvg.Render(Yaml,
            new SvgRenderOptions
            {
                Style = WithEdges(e => e with { BaseColorPalette = ["var(--pl-a)", "var(--pl-b)"] }),
            });

        var strokes = Matches(_baseEdgeStroke, svg);
        Assert.Equal(new[] { "var(--beck-danger)" }, strokes);
    }

    // An explicit author color: on a message wins even when its value coincides with the worker
    // participant's default accent — provenance is the EdgeModel.ColorAuthored flag, never a value
    // comparison (which would mis-read the coincidence as "default" and palette-recolour it).
    [Fact]
    public void BaseColorPalette_SequenceAuthoredMessageColorWins_EvenWhenItEqualsADefault()
    {
        const string Yaml = """
            type: sequence
            participants:
              - { id: a, title: A }
              - { id: b, title: B }
            messages:
              - { from: a, to: b, label: go, color: primary }
            """;
        var svg = BeckSvg.Render(Yaml,
            new SvgRenderOptions
            {
                Style = WithEdges(e => e with { BaseColorPalette = ["var(--pl-a)", "var(--pl-b)"] }),
            });

        // `color: primary` equals participant b's kind-default accent (Service → primary); the
        // authored colour must survive, not become source a's palette hue var(--pl-a).
        var strokes = Matches(_baseEdgeStroke, svg);
        Assert.Equal(new[] { "var(--beck-primary)" }, strokes);
    }

    // ---- underlay layer (static trace bed) ----

    // A trace-bed underlay drops one additional path PER edge whose d is byte-identical to the edge's own
    // d (a wider/darker layer sharing the geometry, never a split of the single continuous edge path),
    // and each bed sits immediately BEFORE its base edge in document order (behind it when painted).
    [Fact]
    public void Underlay_SharesEdgeD_BeforeBase_OnePerEdge()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { UnderlayWidth = 4 }) });

        var baseD = Matches(_baseEdgeD, svg);
        var bedD = Matches(_bedD, svg);
        Assert.NotEmpty(bedD);
        Assert.Equal(baseD.Count, bedD.Count);
        foreach (var bd in bedD)
        {
            Assert.Contains(bd, baseD);
        }

        // Document order: every bed path precedes the matching base edge path (bed is painted behind).
        foreach (Match bed in _bedD.Matches(svg))
        {
            var basePos = svg.IndexOf($"<path class=\"beck-edge beck-edge--", bed.Index, StringComparison.Ordinal);
            Assert.True(basePos > bed.Index, "each trace bed must sit before (behind) its base edge");
            // No other base edge path is interleaved between this bed and its base edge.
            Assert.DoesNotContain("<path class=\"beck-edge beck-edge--",
                svg.Substring(bed.Index + bed.Length, basePos - (bed.Index + bed.Length)));
        }
    }

    // The bed uses the wider underlay stroke width and the fallback bed token colour when UnderlayColor is
    // unset; classic (UnderlayWidth 0) emits no bed at all — the seam is byte-inert off.
    [Fact]
    public void Underlay_WidthAndColor_OffByDefault()
    {
        var yaml = Yaml("arch-kitchen.yaml");
        var bedded = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { UnderlayWidth = 4 }) });
        Assert.Contains("<path class=\"beck-edge-bed\"", bedded);
        Assert.Contains("stroke:var(--beck-edge-underlay, var(--beck-edge));stroke-width:4;", bedded);

        var classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.DoesNotContain("beck-edge-bed", classic);
    }

    // The bed applies to sequence messages AND lifelines (the mock beds those too), each sharing the base
    // element's geometry and sitting before it.
    [Fact]
    public void Underlay_OnSequenceMessagesAndLifelines()
    {
        var svg = BeckSvg.Render(Yaml("sample-sequence.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { UnderlayWidth = 4 }) });

        // Messages: bed paths share the message d.
        var baseD = Matches(_baseEdgeD, svg);
        var bedD = Matches(_bedD, svg);
        Assert.NotEmpty(bedD);
        foreach (var bd in bedD)
        {
            Assert.Contains(bd, baseD);
        }

        // Lifelines: a bed line per lifeline, before its base line.
        Assert.Contains("<line class=\"beck-lifeline-bed\"", svg);
        var bedPos = svg.IndexOf("<line class=\"beck-lifeline-bed\"", StringComparison.Ordinal);
        var basePos = svg.IndexOf("<line class=\"beck-lifeline\"", StringComparison.Ordinal);
        Assert.True(bedPos < basePos, "the lifeline bed must sit before (behind) the base lifeline");

        var classic = BeckSvg.Render(Yaml("sample-sequence.yaml"), new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.DoesNotContain("beck-edge-bed", classic);
        Assert.DoesNotContain("beck-lifeline-bed", classic);
    }

    // ---- path bow ----

    // Bowing changes the drawn geometry but keeps every edge's two endpoints exactly, and the edge is
    // still ONE continuous path (a single M). Compared edge-for-edge against the classic (unbowed) render.
    [Fact]
    public void Bow_PreservesEndpoints_OnePath()
    {
        var yaml = Yaml("arch-kitchen.yaml");
        var classic = Matches(_baseEdgeD, BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));
        var bowed = Matches(_baseEdgeD, BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { BowAmplitude = 6 }) }));

        Assert.Equal(classic.Count, bowed.Count);
        Assert.NotEmpty(bowed);
        var anyChanged = false;
        for (var i = 0; i < classic.Count; i++)
        {
            Assert.Equal(First(classic[i]).X, First(bowed[i]).X, 2);
            Assert.Equal(First(classic[i]).Y, First(bowed[i]).Y, 2);
            Assert.Equal(Last(classic[i]).X, Last(bowed[i]).X, 2);
            Assert.Equal(Last(classic[i]).Y, Last(bowed[i]).Y, 2);
            Assert.Equal(1, bowed[i].Count(ch => ch == 'M'));       // still one continuous path
            if (classic[i] != bowed[i])
            {
                anyChanged = true;
            }
        }
        Assert.True(anyChanged, "bow should perturb at least one edge's geometry");
    }

    // The bow primitive is deterministic and preserves the two endpoints of a straight run, replacing it
    // with a quadratic (Q) through a displaced midpoint.
    [Fact]
    public void BowLine_PreservesEndpoints_IsCurvedAndDeterministic()
    {
        var a = Shaping.BowLine(104, 53, 126, 53, 5, "seed-1");
        var b = Shaping.BowLine(104, 53, 126, 53, 5, "seed-1");
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
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Arrow = EdgeArrow.OpenV }) });

        Assert.Contains("<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"1.5\" stroke=", svg);
        Assert.Contains("<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"8.5\" stroke=", svg);
        Assert.DoesNotContain("points=\"0,1 10,5 0,9\"", svg);     // the classic filled arrow is gone
    }

    // Classic keeps the filled polygon arrowhead.
    [Fact]
    public void Classic_FilledArrow()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"), new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.Contains("points=\"0,1 10,5 0,9\"", svg);
    }

    // Chevron (terminal): the filled arrowhead becomes a mono `>` — TWO hard butt-capped strokes running
    // back from the tip. Stroke-based (no filled triangle polygon), and distinct from OpenV's round caps.
    [Fact]
    public void Chevron_EmitsTwoButtStrokes_NoFilledTriangle()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Arrow = EdgeArrow.Chevron }) });

        Assert.Contains("<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"1.5\" stroke=\"var(--beck-edge)\" stroke-width=\"1.6\" stroke-linecap=\"butt\"/>", svg);
        Assert.Contains("<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"8.5\" stroke=\"var(--beck-edge)\" stroke-width=\"1.6\" stroke-linecap=\"butt\"/>", svg);
        Assert.DoesNotContain("points=\"0,1 10,5 0,9\"", svg);     // no classic filled polygon arrowhead
        // Butt caps, not OpenV's round — the two treatments never collapse to the same marker body.
        Assert.DoesNotContain("stroke-width=\"1.8\" stroke-linecap=\"round\"/>", svg);
    }

    // The chevron marker carries orient="auto-start-reverse", so the ONE def serves both ends: a reply's
    // reversed path draws the same glyph as `<`. (The flip is the marker orient, not a second def.)
    [Fact]
    public void Chevron_OrientsAutoStartReverse_OneDefPerColor()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Arrow = EdgeArrow.Chevron }) });

        var markerDefs = Regex.Matches(svg, "<marker id=\"beck-arrow-[^\"]*\"[^>]*>");
        Assert.NotEmpty(markerDefs);
        foreach (Match m in markerDefs)
        {
            Assert.Contains("orient=\"auto-start-reverse\"", m.Value);
        }
    }

    // The chevron's dedupe key is stable: same shape+color collapses to a single marker def (one `<marker>`
    // per distinct color), the arch corpus uses one edge color so exactly one chevron def is emitted.
    [Fact]
    public void Chevron_DedupeKey_Stable_OneDef()
    {
        var yaml = Yaml("arch-kitchen.yaml");
        var options = new SvgRenderOptions { Style = WithEdges(e => e with { Arrow = EdgeArrow.Chevron }) };
        var a = BeckSvg.Render(yaml, options);
        var b = BeckSvg.Render(yaml, options);
        Assert.Equal(a, b);                                        // deterministic

        // Exactly one chevron marker def (butt-capped stroke pair) despite many edges reusing it.
        var defs = Regex.Matches(a, "stroke-linecap=\"butt\"/></marker>").Count;
        Assert.Equal(1, defs);
    }

    // ---- marker scaling ----

    // MarkerScale multiplies the marker geometry in the default strokeWidth units (classic arrow = 6 → 12
    // at ×2), with no markerUnits switch.
    [Fact]
    public void MarkerScale_MultipliesGeometry()
    {
        var yaml = Yaml("arch-kitchen.yaml");
        var classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        var scaled = BeckSvg.Render(yaml, new SvgRenderOptions { Style = WithEdges(e => e with { MarkerScale = 2 }) });

        Assert.Contains("markerWidth=\"6\"", classic);
        Assert.Contains("markerWidth=\"12\"", scaled);
        Assert.DoesNotContain("markerUnits", scaled);              // still strokeWidth units
    }

    // MarkerScaleToWidth switches to userSpaceOnUse and grows the marker sub-linearly with the edge
    // stroke width — the "sane scaling" a thick line needs. Classic never emits markerUnits.
    [Fact]
    public void MarkerScaleToWidth_UsesUserSpaceOnUse()
    {
        var yaml = Yaml("arch-kitchen.yaml");
        Assert.DoesNotContain("markerUnits", BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));

        var svg = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { MarkerScaleToWidth = true }) });
        Assert.Contains("markerUnits=\"userSpaceOnUse\"", svg);
        // The arrow's classic base width is 6; scaled to width it grows (>6).
        var w = Matches(_markerW, svg).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).Max();
        Assert.True(w > 6, $"expected a grown marker width, got {w}");
    }

    // MarkerOutline (brutalist's lime-fill/white-outline arrowhead): the plain filled arrow polygon gains
    // a contrast stroke + stroke-width 1.5, and its marker is drawn overflow="visible" so the outline isn't
    // clipped. Classic (unset) keeps the flat single-colour fill and never emits the outline/overflow.
    [Fact]
    public void MarkerOutline_StrokesFilledArrow_OverflowVisible()
    {
        var yaml = Yaml("arch-kitchen.yaml");
        var outlined = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { MarkerOutline = "var(--beck-edge)" }) });
        // The filled polygon now carries the outline stroke; the marker element is overflow-visible.
        Assert.Contains("<polygon points=\"0,1 10,5 0,9\" fill=\"var(--beck-edge)\" stroke=\"var(--beck-edge)\" stroke-width=\"1.5\" stroke-linejoin=\"round\"/>", outlined);
        Assert.Contains("overflow=\"visible\"", outlined);

        // Classic: flat fill, no outline stroke on the arrow polygon, no overflow attribute.
        var classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.Contains("<polygon points=\"0,1 10,5 0,9\" fill=\"var(--beck-edge)\"/>", classic);
        Assert.DoesNotContain("overflow=\"visible\"", classic);
    }

    // MarkerOutline only decorates the plain filled arrowhead: it is inert for the open-V / chevron
    // treatments (which are stroke-pairs, not a filled polygon), so those never gain overflow="visible".
    [Fact]
    public void MarkerOutline_InertForOpenVAndChevron()
    {
        var yaml = Yaml("arch-kitchen.yaml");
        var openV = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { Arrow = EdgeArrow.OpenV, MarkerOutline = "var(--beck-edge)" }) });
        Assert.DoesNotContain("overflow=\"visible\"", openV);
        Assert.DoesNotContain("<polygon points=\"0,1 10,5 0,9\"", openV);
    }

    // ---- base-layer treatment ----

    [Fact]
    public void BaseOpacity_And_BaseLinecap()
    {
        var yaml = Yaml("arch-kitchen.yaml");
        var faint = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { BaseOpacity = 0.35, BaseLinecap = "butt" }) });
        Assert.Contains("stroke-opacity:0.35", faint);
        Assert.Contains(".beck-edge{fill:none;stroke-width:1.6;stroke-linecap:butt;", faint);

        var classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });
        Assert.DoesNotContain("stroke-opacity:0.35", classic);    // no base-edge opacity by default
        Assert.Contains(".beck-edge{fill:none;stroke-width:1.6;stroke-linecap:round;", classic);
    }

    // ---- lifelines / separators ----

    [Fact]
    public void Lifeline_FaintSolid_DropsDash()
    {
        var yaml = Yaml("sample-sequence.yaml");
        Assert.Contains("stroke-dasharray:6 7", BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));

        var solid = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { Lifeline = LifelineShape.FaintSolid }) });
        Assert.Contains(".beck-lifeline{stroke-width:2;}", solid);
    }

    [Fact]
    public void Lifeline_Wobbly_SwapsLineForPath()
    {
        var yaml = Yaml("sample-sequence.yaml");
        Assert.Contains("<line class=\"beck-lifeline\"", BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));

        var wobbly = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { Lifeline = LifelineShape.Wobbly }) });
        Assert.Contains("<path class=\"beck-lifeline\"", wobbly);
        Assert.DoesNotContain("<line class=\"beck-lifeline\"", wobbly);
    }

    [Fact]
    public void WobblySeparators_SwapClassLinesForPaths()
    {
        var yaml = Yaml("class.yaml");
        Assert.Contains("<line class=\"beck-class-head-border\"", BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic }));

        var wobbly = BeckSvg.Render(yaml,
            new SvgRenderOptions { Style = WithEdges(e => e with { WobblySeparators = true }) });
        Assert.Contains("<path class=\"beck-class-head-border\"", wobbly);
        Assert.DoesNotContain("<line class=\"beck-class-head-border\"", wobbly);
    }

    // ---- blueprint (mock 1a): every connector is dashed + perpetually flowing ----

    // Blueprint's headline motion is a Marching overlay on EVERY architecture edge and sequence
    // message: a 6 6 dash (mirroring the mock's `stroke-dasharray:6 6`) that marches on a 1.6s
    // compiled shared-cycle loop (the mock's `animation:df 1.6s linear infinite` verbatim), sharing
    // each base edge's exact d, no delay chain.
    [Fact]
    public void Blueprint_MarchesOnEveryArchitectureEdge()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = BlueprintStyle.Instance });

        var baseD = Matches(_baseEdgeD, svg);
        var overlayD = Matches(_overlayD, svg);
        Assert.NotEmpty(baseD);
        Assert.Equal(baseD.Count, overlayD.Count);
        foreach (var od in overlayD)
        {
            Assert.Contains(od, baseD);
        }

        Assert.Contains("stroke-dasharray:6 6", svg);
        Assert.Matches(@"\.beo0-[0-9a-z]+\{animation:kbeo0-[0-9a-z]+ 1\.6s linear infinite;\}", svg);
        Assert.Contains("@keyframes kbeo0-", svg);
        Assert.DoesNotContain("animation-delay", svg);
    }

    // The same marching overlay also rides sequence messages, not just architecture edges.
    [Fact]
    public void Blueprint_MarchesOnSequenceMessagesToo()
    {
        var svg = BeckSvg.Render(Yaml("sample-sequence.yaml"),
            new SvgRenderOptions { Style = BlueprintStyle.Instance });
        var baseD = Matches(_baseEdgeD, svg);
        var overlayD = Matches(_overlayD, svg);
        Assert.NotEmpty(overlayD);
        foreach (var od in overlayD)
        {
            Assert.Contains(od, baseD);
        }
    }

    // ---- draw-on: every connector inks itself in slowly, on architecture AND sequence ----

    // The DrawOn overlay (sketch's headline motion, available to any style) inks EVERY edge —
    // architecture, class, and sequence messages all get an overlay path sharing the base edge's
    // exact d, on a compiled shared-cycle wipe.
    [Fact]
    public void DrawOn_InksEveryArchitectureEdge()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = WithEdges(e => e with { Overlay = EdgeOverlay.DrawOn }) });

        var baseD = Matches(_baseEdgeD, svg);
        var overlayD = Matches(_overlayD, svg);
        Assert.NotEmpty(baseD);
        Assert.Equal(baseD.Count, overlayD.Count);
        foreach (var od in overlayD)
        {
            Assert.Contains(od, baseD);
        }

        Assert.Contains("@keyframes kbeo0-", svg);
        Assert.DoesNotContain("animation-delay", svg);
    }

    // The same draw-on also inks class-diagram relations and sequence messages, not only the
    // architecture graph. class.yaml in the corpus has no authored flow, so the model builder turns
    // animation off entirely (Model/ClassBuilder.cs — unrelated to the style seam); a minimal
    // `meta.animate: true` class diagram isolates the style's own behaviour from that model-level
    // default.
    [Fact]
    public void DrawOn_InksClassAndSequenceEdgesToo()
    {
        const string ClassYaml = """
            type: class
            meta:
              title: Demo
              animate: true
            classes:
              - id: a
                name: A
              - id: b
                name: B
            relations:
              - from: a
                to: b
                kind: association
            flow:
              steps: []
            """;
        var drawOn = WithEdges(e => e with { Overlay = EdgeOverlay.DrawOn });
        var classSvg = BeckSvg.Render(ClassYaml, new SvgRenderOptions { Style = drawOn });
        Assert.NotEmpty(Matches(_overlayD, classSvg));

        var seqSvg = BeckSvg.Render(Yaml("sample-sequence.yaml"), new SvgRenderOptions { Style = drawOn });
        Assert.NotEmpty(Matches(_overlayD, seqSvg));
    }

    // ---- terminal (mock 1f): a stepped phosphor block ticks down every wire ----

    // Terminal's headline edge motion is a Comet overlay on EVERY architecture edge, ticking in 12 hard
    // discrete steps (the mock's `stroke-width:5;stroke-dasharray:5 298;animation:ptd 1.6s steps(12)
    // infinite`): each overlay path shares its base edge's exact d, is a squared (butt) 5px phosphor block
    // on the palette-less --beck-edge-overlay fallback hue, and the loop is compiled onto the shared cycle
    // with no delay chain.
    [Fact]
    public void Terminal_SteppedPhosphorBlockOnEveryWire_SharesD_NoDelayChain()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = TerminalStyle.Instance });

        var baseD = Matches(_baseEdgeD, svg);
        var overlayD = Matches(_overlayD, svg);
        Assert.NotEmpty(overlayD);
        Assert.Equal(baseD.Count, overlayD.Count);
        foreach (var od in overlayD)
        {
            Assert.Contains(od, baseD);
        }

        // Stepped tick (steps(12)) on a compiled 1.6s shared-cycle loop, guarded, no delay chain.
        Assert.Matches(@"\.beo0-[0-9a-z]+\{animation:kbeo0-[0-9a-z]+ 1\.6s steps\(12\) infinite;\}", svg);
        Assert.Contains("@keyframes kbeo0-", svg);
        Assert.Contains("@media (prefers-reduced-motion:no-preference)", svg);
        Assert.DoesNotContain("animation-delay", svg);

        // The block is a squared (butt) 5px phosphor dash on the single overlay fallback hue (no palette).
        Assert.Contains("stroke:var(--beck-edge-overlay, var(--beck-accent));stroke-width:5;stroke-linecap:butt;", svg);
    }

    // The mock draws the `>` chevron BRIGHT (`#4ade80`) over a DIM green wire (`#166534`): terminal keeps
    // every default edge on its var(--beck-edge) token (the dim-green trace) while MarkerColor lifts the
    // chevron marker to the bright var(--beck-accent). The two never collapse to one hue.
    [Fact]
    public void Terminal_ChevronRidesBrightAccentOverDefaultWire()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = TerminalStyle.Instance });

        // Base edges stay on their token hue — the default wires on the dim-green var(--beck-edge), a
        // kind-default (dependency) edge on var(--beck-neutral) — and NONE on the bright accent: the wire
        // is always the dim trace, never the phosphor.
        var strokes = Matches(_baseEdgeStroke, svg);
        Assert.NotEmpty(strokes);
        Assert.Contains("var(--beck-edge)", strokes);
        Assert.DoesNotContain("var(--beck-accent)", strokes);

        // The chevron marker (two butt-capped strokes) is drawn in the bright accent, not the wire's hue.
        Assert.Contains("<line x1=\"10\" y1=\"5\" x2=\"2\" y2=\"1.5\" stroke=\"var(--beck-accent)\" stroke-width=\"1.6\" stroke-linecap=\"butt\"/>", svg);
        // No chevron stroke falls back to the dim edge colour.
        Assert.DoesNotContain("stroke=\"var(--beck-edge)\" stroke-width=\"1.6\" stroke-linecap=\"butt\"", svg);
    }

    // The travelling flow packet HOPS in hard steps too (the mock steps its flow): PacketSteps=12 puts a
    // steps(12) timing on the packet's own offset-distance track, while the trail keeps its own TrailSteps
    // hard-cut reveal. Classic keeps the smooth per-edge-kind ease.
    [Fact]
    public void Terminal_PacketFlowStepsInHardCuts_ClassicDoesNot()
    {
        var yaml = Yaml("arch-kitchen.yaml");
        var terminal = BeckSvg.Render(yaml, new SvgRenderOptions { Style = TerminalStyle.Instance });
        var classic = BeckSvg.Render(yaml, new SvgRenderOptions { Style = BeckStyle.Classic });

        var n = TerminalStyle.Instance.Motion.PacketSteps!.Value;
        Assert.Equal(12, n);
        Assert.Contains($"animation-timing-function:steps({n})", terminal);
        Assert.DoesNotContain("animation-timing-function:steps(", classic);
    }

    // ---- circuit (mock 1h): an amber signal pulses along every right-angle trace ----

    // Circuit's headline edge motion is a Comet overlay on EVERY architecture edge and sequence message:
    // an 8px gold dot (the mock's `stroke:#fcd34d;stroke-width:3;stroke-linecap:round;
    // stroke-dasharray:8 304;animation:pt 2.2s linear infinite`) gliding each trace on a 2.2s compiled
    // shared-cycle loop. Each overlay path shares its base edge's exact d, rides the palette-less
    // --beck-edge-overlay fallback hue, and the loop has no delay chain and is reduced-motion guarded.
    [Fact]
    public void Circuit_AmberSignalCometOnEveryTrace_SharesD_NoDelayChain()
    {
        var svg = BeckSvg.Render(Yaml("arch-kitchen.yaml"),
            new SvgRenderOptions { Style = CircuitStyle.Instance });

        var baseD = Matches(_baseEdgeD, svg);
        var overlayD = Matches(_overlayD, svg);
        Assert.NotEmpty(overlayD);
        Assert.Equal(baseD.Count, overlayD.Count);
        foreach (var od in overlayD)
        {
            Assert.Contains(od, baseD);
        }

        // A width-3 round-capped comet on the single overlay fallback hue (no palette), an 8px lit dash.
        Assert.Contains("stroke:var(--beck-edge-overlay, var(--beck-accent));stroke-width:3;stroke-linecap:round;", svg);
        Assert.Contains("stroke-dasharray:8 ", svg);

        // Compiled 2.2s shared-cycle loop, guarded, glides linear (not stepped), no delay chain.
        Assert.Matches(@"\.beo0-[0-9a-z]+\{animation:kbeo0-[0-9a-z]+ 2\.2s linear infinite;\}", svg);
        Assert.Contains("@keyframes kbeo0-", svg);
        Assert.Contains("@media (prefers-reduced-motion:no-preference)", svg);
        Assert.DoesNotContain("animation-delay", svg);
    }

    // The same amber comet rides sequence messages too, sharing each message path's d (the two-layer trace
    // beds them as well) — the pulse is the ambient identity on every trace, not just architecture edges.
    [Fact]
    public void Circuit_AmberSignalCometOnSequenceMessages()
    {
        var svg = BeckSvg.Render(Yaml("sample-sequence.yaml"),
            new SvgRenderOptions { Style = CircuitStyle.Instance });
        var baseD = Matches(_baseEdgeD, svg);
        var overlayD = Matches(_overlayD, svg);
        Assert.NotEmpty(overlayD);
        foreach (var od in overlayD)
        {
            Assert.Contains(od, baseD);
        }
    }
}