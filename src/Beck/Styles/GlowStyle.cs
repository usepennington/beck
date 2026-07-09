namespace Beck;

/// <summary>
/// The <c>glow</c> built-in style (mock 1g) — the design brief's "baseline to beat", rebuilt around the
/// per-style <em>edge-presentation</em> seam (<see cref="StyleEdges"/>) so its identity is carried by the
/// edge treatment + glass surfaces + bloom, not by token tweaks alone:
/// <list type="bullet">
/// <item><b>Two-layer edges.</b> A faint slate <em>base rail</em> (the default <c>--beck-edge</c> stroke
/// at width 1, <see cref="StyleEdges.BaseOpacity"/> dimmed; replies/dashed at <c>3 4</c>) under a bright
/// <em>comet</em> overlay (<see cref="EdgeOverlay.Comet"/>, ~2.5px round-capped, a 10px lit dash that
/// travels the whole path). The comet is glow's ambient packet/trail presentation — an additional path
/// sharing the edge's exact <c>d</c>, its per-edge hue alternating cyan/light-cyan/violet
/// (<see cref="StyleEdges.OverlayPalette"/>) and its phase baked from the content hash, compiled onto a
/// shared-cycle <c>linear infinite</c> loop by <c>CssCompiler.EdgeOverlayCss</c> (no delay chain) and
/// killed under reduced motion. The comet blooms via <see cref="StyleEdges.OverlayBloom"/>; arrowheads are
/// small filled triangles in the comet hue (<see cref="StyleEdges.MarkerColor"/>).</item>
/// <item><b>Glass nodes.</b> Very translucent slate surfaces (<c>--beck-node-bg</c>) rimmed with a real
/// cyan→violet <c>&lt;linearGradient&gt;</c> stroke (<see cref="StyleStrokes.GradientNodes"/>, ~1.2px), the
/// flow-active node brightened + bloomed + breathing through the existing pulse/highlight windows
/// (stretched to read as a breath).</item>
/// <item><b>Sequence scenery.</b> Faint <em>solid</em> 1px lifelines (<see cref="LifelineShape.FaintSolid"/>),
/// strokeless bloomed activation bars (inherited), mono members / muted-slate labels.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para><b>Seam vs. glow-specific.</b> Everything above rides the generalizable seam: <see cref="StyleEdges"/>
/// (base opacity, comet overlay + palette + bloom, marker colour, faint-solid lifeline), the reusable node
/// gradient (<see cref="StyleStrokes.GradientNodes"/> + the shared <c>beck-node-grad-{hash}</c> def), and the
/// classic token/mix/motion fields. No artwork branch, no bespoke markup — a custom style could compose the
/// same look. Deriving from <see cref="BeckStyle.Classic"/> with a <c>with</c> expression keeps every feature
/// available by construction.</para>
/// <para><b>Dark is home turf; light stays legible.</b> The light table keeps a white surface, a slate base
/// rail and glass panels tasteful on white, and the lifted sequence dim floors so a static viewer can still
/// read a dimmed row. The dark overrides drop to a deep night surface and brighten the accents a ramp step so
/// the gradient/comet/bloom read as light. Every entry keeps the three-tier
/// <c>var(--beck-X, var(--color-Y, literal))</c> indirection, so a host palette always wins.</para>
/// </remarks>
public static class GlowStyle
{
    public static readonly BeckStyle Instance = Build();

    private static BeckStyle Build()
    {
        BeckStyle c = BeckStyle.Classic;

        // Light table. Glass node surface (very translucent slate); a faint slate base rail; an indigo/
        // violet accent ramp for the bloom + active read; and the cyan→violet gradient / cyan·light-cyan·
        // violet comet hues carried by dedicated --beck-* tokens over the host ramp. Surfaces stay light so
        // the style still adopts a light host page.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #ffffff)"),
            ("--beck-node-bg", "color-mix(in srgb, var(--color-base-400, #94a3b8) 7%, transparent)"),
            ("--beck-node-border", "color-mix(in srgb, var(--beck-accent) 30%, var(--color-base-200, #e2e8f0))"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.05), 0 4px 12px rgb(0 0 0 / 0.06)"),
            ("--beck-text", "var(--color-base-800, #1e293b)"),
            ("--beck-text-muted", "var(--color-base-500, #64748b)"),
            ("--beck-text-faint", "var(--color-base-400, #94a3b8)"),
            ("--beck-primary", "var(--color-primary-500, #6366f1)"),
            ("--beck-success", "var(--color-emerald-500, #10b981)"),
            ("--beck-warn", "var(--color-amber-500, #f59e0b)"),
            ("--beck-danger", "var(--color-red-500, #ef4444)"),
            ("--beck-info", "var(--color-violet-500, #a855f7)"),
            ("--beck-neutral", "var(--color-base-400, #94a3b8)"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {P(c.Mix.GroupBorder)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text-muted)"),
            // A step darker than base-400 so the faint comet base rail actually reads on a white page
            // (the previous base-400 rail was near-invisible in light mode); still a tasteful slate.
            ("--beck-edge", "var(--color-base-500, #64748b)"),
            ("--beck-packet", "var(--beck-accent)"),
            ("--beck-icon-bg", "color-mix(in srgb, var(--beck-accent) 8%, var(--color-base-100, #f1f5f9))"),
            ("--beck-accent", "var(--beck-primary)"),
            // glow hues (cyan / light-cyan / violet) — the gradient endpoints + comet palette.
            ("--beck-node-grad-a", "var(--color-cyan-400, #22d3ee)"),
            ("--beck-node-grad-b", "var(--color-violet-400, #a78bfa)"),
            ("--beck-comet-1", "var(--color-cyan-400, #22d3ee)"),
            ("--beck-comet-2", "var(--color-cyan-300, #67e8f9)"),
            ("--beck-comet-3", "var(--color-violet-400, #a78bfa)"),
        });

        // Dark is the showpiece: a deep night surface, a ramp-step-brighter accent ramp so bloom + gradient
        // read as light, and a slightly brighter slate rail. The glow hues stay vivid on both themes, so
        // they are not overridden here.
        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #080b16)"),
            ("--beck-node-bg", "color-mix(in srgb, var(--color-base-400, #94a3b8) 8%, transparent)"),
            ("--beck-node-border", "color-mix(in srgb, var(--beck-accent) 42%, var(--color-base-700, #30363d))"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.3), 0 4px 14px rgb(0 0 0 / 0.4)"),
            ("--beck-text", "var(--color-base-50, #f0f6fc)"),
            ("--beck-text-muted", "var(--color-base-400, #8b949e)"),
            ("--beck-text-faint", "var(--color-base-500, #6e7681)"),
            ("--beck-primary", "var(--color-primary-400, #818cf8)"),
            ("--beck-info", "var(--color-violet-400, #c084fc)"),
            ("--beck-edge", "var(--color-base-500, #64748b)"),
            ("--beck-icon-bg", "color-mix(in srgb, var(--beck-accent) 16%, var(--color-base-800, #21262d))"),
        });

        StyleGeometry geo = c.Geometry with
        {
            // Softer corners so the bloom haloes a rounded edge.
            CardRadius = 16,
            ClassRadius = 14,
            GhostRadius = 18,
            GroupRadius = 20,
            IconChipRadius = 10,
            NarrationRadius = 14,
            BandRadius = 16,

            // Thin, luminous chrome (mock 1g): a 1.2px gradient node rim, width-1 base edge/message rails
            // under the 2.5px comet, and faint 1px lifelines. NodeStroke 1.2 keeps MeasureBorder at 2
            // (2·round(1.2/2)) — identical to classic's card-sizing budget — so no card golden drifts.
            NodeStroke = 1.2,
            EdgeStroke = 1,
            MessageStroke = 1,
            LifelineStroke = 1,

            // Accent bloom via the card drop-shadow filter (bloom colour = each node's own --beck-accent):
            // a crisp rim, a medium halo, and a wide low-alpha wash. Light holds a lower-alpha wash; dark —
            // glow's home turf — pushes it hardest.
            NodeShadow =
                "drop-shadow(0 0 1px color-mix(in srgb, var(--beck-accent) 52%, transparent)) " +
                "drop-shadow(0 0 10px color-mix(in srgb, var(--beck-accent) 34%, transparent)) " +
                "drop-shadow(0 3px 16px color-mix(in srgb, var(--beck-accent) 20%, transparent)) " +
                "drop-shadow(0 1px 2px rgb(0 0 0/.06))",
            NodeShadowDark =
                "drop-shadow(0 0 2px color-mix(in srgb, var(--beck-accent) 58%, transparent)) " +
                "drop-shadow(0 0 14px color-mix(in srgb, var(--beck-accent) 40%, transparent)) " +
                "drop-shadow(0 0 30px color-mix(in srgb, var(--beck-accent) 24%, transparent)) " +
                "drop-shadow(0 2px 10px rgb(0 0 0/.55))",
            NarrationShadow =
                "drop-shadow(0 0 16px color-mix(in srgb, var(--beck-accent) 20%, transparent)) " +
                "drop-shadow(0 4px 12px rgb(0 0 0/.08))",
        };

        StyleMix mix = c.Mix with
        {
            // Brighter icon chips + activation glow so the luminous read carries into class cards and
            // sequence activation bars. (NodeStroke mix is unused for node surfaces now — the gradient rim
            // replaces it — but stays set for any accent-mix consumer.)
            NodeStroke = 50,
            IconChip = 24,
            ActivationGlow = 66,
        };

        StyleStrokes strokes = c.Strokes with
        {
            // Real cyan→violet gradient rim on every node surface (shared beck-node-grad def).
            GradientNodes = true,
            // Replies / author-dashed edges: the mock's 3 4 dash on the faint base rail.
            EdgeDash = "3 4",
        };

        StyleMotion motion = c.Motion with
        {
            // Breathing pulse on the flow-active node: classic's snap (0.6/0.7s) stretched to a slower
            // rise-and-settle. Still flow-active-only and compiled onto the shared cycle (no always-on loop).
            PulseDur = 1.0,
            HighlightDur = 1.2,
            // Lifted sequence dim floors: glow's soft accents/edges paint the resting (pre-reveal) frame at
            // these opacities; classic's 0.15/0.35/0.45 drop below legibility on glow-light's white surface.
            // A static viewer keeps reading the dimmed rows while the reveal still swells each active row to
            // full — a clear storytelling contrast remains.
            DimLine = 0.42,
            DimLabel = 0.58,
            DimBand = 0.6,
            // Packet bloom stays on (core to glow) with a wider blur so the travelling dot carries a soft halo.
            GlowEnabled = true,
            PacketGlowBlur = 5,
            // The bloom ripple (mock 1g's glowing `ringx`): arrivals expand the classic ring but
            // carried on a drop-shadow halo, swelling further before it fades.
            Pulse = PulseEffect.GlowRing,
        };

        StyleEdges edges = c.Edges with
        {
            // Faint slate base rail under the bright comet. 0.4 (vs the mock's dark-only .35) keeps the rail
            // legible on both themes.
            BaseOpacity = 0.4,
            // The travelling comet — glow's ambient packet/trail presentation on every edge/message.
            // A longer lit dash + a touch more width so the comet reads clearly on the long architecture
            // edges too (a 10px dot got lost on a ~100px hop where the short sequence messages carried it
            // fine); still a single travelling comet, just with more presence in the static frame.
            Overlay = EdgeOverlay.Comet,
            OverlayWidth = 3,
            OverlayLinecap = "round",
            CometDash = 15,
            OverlayPeriod = 2.4,
            // Per-edge hue alternation cyan → light-cyan → violet.
            OverlayPalette = new[] { "var(--beck-comet-1)", "var(--beck-comet-2)", "var(--beck-comet-3)" },
            // Comet bloom — a fixed cyan halo on the bright overlay (keeps labels crisp; matches the mock's
            // group drop-shadow read without blooming text).
            OverlayBloom = "drop-shadow(0 0 6px color-mix(in srgb, var(--beck-comet-1) 55%, transparent))",
            // Arrowheads: small filled triangles in the comet hue over the faint base rail.
            MarkerColor = "var(--beck-comet-2)",
            // Faint SOLID 1px lifelines (not dashed).
            Lifeline = LifelineShape.FaintSolid,
        };

        return c with
        {
            Name = "glow",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Mix = mix,
            Strokes = strokes,
            Motion = motion,
            Edges = edges,
        };
    }

    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
