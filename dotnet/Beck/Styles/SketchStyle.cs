using Beck.Rendering.Text;

namespace Beck;

/// <summary>
/// The <c>sketch</c> built-in style (Phase 4, artwork + edge presentation): a hand-drawn look — warm-paper
/// tokens, <em>Shantell Sans</em> throughout, <c>fill:none</c> outline node surfaces with per-node
/// corner-radius variety, and — the star of the style — a full hand-drawn <em>edge treatment</em> from the
/// edge-presentation seam (<see cref="StyleEdges"/>): subtly-bowed curved connectors with round caps,
/// open-V arrowheads, edges that redraw themselves once per cycle, and wobbly dashed lifelines / class
/// separators. Derived from <see cref="BeckStyle.Classic"/> with a <c>with</c> expression, so every feature
/// (all shapes/variants, groups, icons, edges + labels + UML markers, packets + labels, trails,
/// highlight/pulse/fail, status pills, narration, impact/working rings, sequence choreography, state/class
/// diagrams, scrub, reduced motion, light/dark) stays fully available — only tokens, geometry, typography,
/// the shape family, and the edge presentation change.
/// </summary>
/// <remarks>
/// <para><b>What maps to the shared edge seam</b> (identical machinery every style could reuse):
/// <see cref="StyleEdges.BowAmplitude"/> (deterministic quadratic bow rebuilt from the router's own
/// polyline — endpoints/elbows preserved, still one continuous path), <see cref="EdgeArrow.OpenV"/>
/// (two round-capped strokes for the plain arrowhead; the inheritance triangle / composition diamond keep
/// their closed bodies, already drawn page-bg-fill + ink-stroke + round-join in <c>Markers</c>),
/// <see cref="EdgeOverlay.DrawOn"/> (the "connectors redraw themselves" wipe, compiled onto the shared
/// cycle by <c>CssCompiler.EdgeOverlayCss</c> — reduced-motion shows the fully-drawn base edge),
/// <see cref="LifelineShape.Wobbly"/> + <see cref="StyleEdges.WobblySeparators"/> (sideways-bowed
/// lifelines and class compartment separators, endpoints preserved), plus the <c>4 6</c> lifeline/reply
/// dash on <see cref="StyleStrokes"/>.</para>
/// <para><b>What is a sketch-specific branch</b> (keyed off <see cref="StyleArtwork.Sketch"/>, so every
/// other style stays byte-identical): the wobbly closed-path node/group/pseudo-state outlines and their
/// per-node hash-derived rounding (<see cref="Rendering.Svg.Artwork"/>); the <c>fill:none</c> node surface
/// (token <c>--beck-node-bg: none</c>); the accent-matched node/class title ink and accent class dividers
/// (a small style-gated block in <c>Stylesheet</c>); and the outlined, translucent-accent-filled activation
/// bars (a branch in <c>SequencePainter</c>). The jitter is baked into path geometry, seeded off the
/// content hash + element id, so the same YAML wobbles the same way forever — nothing breathes on a loop.</para>
/// </remarks>
public static class SketchStyle
{
    public static readonly BeckStyle Instance = Build();

    private static BeckStyle Build()
    {
        BeckStyle c = BeckStyle.Classic;

        // Warm-paper token table. Every entry keeps the three-tier var(--beck-X, var(--color-Y, literal))
        // indirection, so a host --color-* / --beck-* palette still wins; only the literal fallbacks warm
        // up (surface/ink/border/edge), while the semantic accents stay classic so status colour meaning
        // holds. node-bg is `none` — the mock's fill:none outline surfaces (edges show through the paper);
        // --beck-edge is pulled up to the muted ink so connectors read as pencil lines (the DrawOn overlay
        // then redraws each edge in accent over that base). The group-border entry threads mix.GroupBorder
        // so the 45% ratio has one source.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #fbf7ef)"),
            ("--beck-node-bg", "none"),
            ("--beck-node-border", "var(--color-base-300, #d8cdb8)"),
            ("--beck-node-shadow", "0 1px 2px rgb(80 64 32 / 0.06), 0 4px 10px rgb(80 64 32 / 0.07)"),
            ("--beck-text", "var(--color-base-800, #40382c)"),
            ("--beck-text-muted", "var(--color-base-500, #7a6f5c)"),
            ("--beck-text-faint", "var(--color-base-400, #a89c86)"),
            ("--beck-primary", "var(--color-primary-600, #5145d8)"),
            ("--beck-success", "var(--color-emerald-500, #10b981)"),
            ("--beck-warn", "var(--color-amber-500, #f59e0b)"),
            ("--beck-danger", "var(--color-red-500, #ef4444)"),
            ("--beck-info", "var(--color-violet-500, #8b5cf6)"),
            ("--beck-neutral", "var(--color-base-400, #a89c86)"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {P(c.Mix.GroupBorder)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text-muted)"),
            ("--beck-edge", "var(--beck-text-muted)"),
            ("--beck-packet", "var(--beck-primary)"),
            ("--beck-icon-bg", "var(--color-base-100, #f3ecdd)"),
            ("--beck-accent", "var(--beck-primary)"),
        });

        // Dark overrides only (layered over the light block, which is emitted first): a warm near-black
        // paper with warm-brown ink for the same hand-drawn feel on a dark page. node-bg and --beck-edge
        // are intentionally NOT overridden here — node-bg stays `none`, and --beck-edge stays the (now
        // per-theme) muted-ink reference so dark edges track the dark ink automatically.
        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #1c1815)"),
            ("--beck-node-border", "var(--color-base-700, #453b2e)"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.4), 0 4px 14px rgb(0 0 0 / 0.5)"),
            ("--beck-text", "var(--color-base-50, #f5efe2)"),
            ("--beck-text-muted", "var(--color-base-400, #b3a893)"),
            ("--beck-text-faint", "var(--color-base-500, #857a66)"),
            ("--beck-icon-bg", "var(--color-base-800, #2f271d)"),
        });

        // Friendly rounded geometry + slightly heavier ink stroke (reads as a felt-tip outline). NodeStroke
        // 2 keeps MeasureBorder at 2 (2·round(2/2)) — identical to classic's budget — so the measured box is
        // unchanged and only the drawn stroke thickens. Card/class/ghost radii drop into the mock's rx 6–9
        // band (8, then per-node hash-varied in the wobble outline); pills keep h/2. Lifeline stroke thins to
        // the mock's 1.2. Soft warm drop-shadows; shadow colours are rgb() literals (shadows are never themed
        // tokens), no resolved colour touches a shape fill/stroke.
        StyleGeometry geo = c.Geometry with
        {
            CardRadius = 8,
            ClassRadius = 8,
            GhostRadius = 8,
            GroupRadius = 18,
            IconChipRadius = 8,
            GroupLabelBgRadius = 4,
            NarrationRadius = 12,
            BandRadius = 14,

            NodeStroke = 2,
            GroupStroke = 2,
            EdgeStroke = 1.8,
            BandBoxStroke = 1.6,
            LifelineStroke = 1.2,
            EndNodeStroke = 2,
            HairlineStroke = 1.4,
            MessageStroke = 1.8,

            NodeShadow = "drop-shadow(0 2px 3px rgb(90 70 35/.10))",
            NodeShadowDark = "drop-shadow(0 2px 4px rgb(0 0 0/.45))",
            NarrationShadow = "drop-shadow(0 2px 6px rgb(90 70 35/.10))",
        };

        // Hand-drawn family stack + the matching embedded metrics table, so boxes measure against Shantell
        // Sans (the host supplies the webfont; the textLength guard absorbs its absence). Roles are classic
        // — Shantell carries the weight hierarchy; the embedded table has weight 400 and the measurer
        // clamps to it, which the guard then fits.
        StyleTypography typography = c.Typography with
        {
            SansFamily = "'Shantell Sans', 'Comic Sans MS', cursive",
            MetricsFont = MetricsFont.ShantellSans,
        };

        // Mix ratios pushed to the mock's bold, flat-fill hand-drawn look:
        //  - NodeStroke 100 → the card outline is the node's pure accent (a felt-tip line), and the
        //    accent-matched title ink (Stylesheet sketch block) reads the same colour → "label matches stroke".
        //  - ClassHead 0 / ClassHeadBorder 100 → no head fill on the fill:none class card, accent separators.
        //  - ChipStroke 0 → message/band chips lose their outline so labels sit bare on the paper (the mock
        //    has no chip boxes around message labels).
        //  - ActivationGlow 0 → the activation bar's drop-shadow glow becomes transparent (no bloom); the
        //    bar itself is redrawn as an outlined translucent-accent rect in SequencePainter's sketch branch.
        StyleMix mix = c.Mix with
        {
            NodeStroke = 100,
            ClassHead = 0,
            ClassHeadBorder = 100,
            ChipStroke = 0,
            ActivationGlow = 0,
        };

        // Dash patterns: the mock's wobbly lifelines and dashed reply messages both march at `4 6`.
        StyleStrokes strokes = c.Strokes with
        {
            LifelineDash = "4 6",
            EdgeDash = "4 6",
        };

        // The hand-drawn edge presentation (the star of the style) — every knob maps to the shared
        // edge-presentation seam, so this is the reference consumer for it:
        //  - BowAmplitude: deterministic quadratic bow on every straight run (endpoints/elbows kept). Tuned
        //    so even short ~100-150px hops visibly bow (a control offset of up to this many px, ~5px median —
        //    matching the mock's ~5px bow over ~110px runs); a lower value left straight hops reading straight.
        //  - Arrow OpenV: two round-capped pen strokes for the plain arrowhead (closed UML ends untouched).
        //  - Overlay DrawOn: the connector redraws itself once per OverlayPeriod (accent wipe over the ink
        //    base), compiled onto the shared cycle; reduced-motion shows the fully-drawn base edge.
        //  - Lifeline Wobbly + WobblySeparators: sideways-bowed lifelines and class compartment separators.
        StyleEdges edges = StyleEdges.Classic with
        {
            BowAmplitude = 10,
            Arrow = EdgeArrow.OpenV,
            Overlay = EdgeOverlay.DrawOn,
            OverlayWidth = 1.8,
            OverlayLinecap = "round",
            OverlayPeriod = 3.0,
            Lifeline = LifelineShape.Wobbly,
            WobblySeparators = true,
        };

        return c with
        {
            Name = "sketch",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Typography = typography,
            Mix = mix,
            Strokes = strokes,
            Edges = edges,
            Artwork = StyleArtwork.Sketch,
        };
    }

    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
