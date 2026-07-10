namespace Beck.Styles;

/// <summary>
/// The <c>circuit</c> built-in style (Phase 4, artwork, mock 1h): a printed-circuit board. Every
/// card/pill/class node keeps its straight rounded rect (so all token-driven fill/stroke/filter still
/// applies) but reads as a socketed <em>chip</em> — short copper <em>pin stubs</em> march down its left
/// and right edges (<see cref="StyleArtwork.Circuit"/>), and every right-angle edge trace drops a small
/// gold <em>via dot</em> at each bend of its already-computed route. An <em>amber signal comet</em> (the
/// edge-presentation seam's <see cref="EdgeOverlay.Comet"/>) pulses along every trace continuously,
/// independent of any flow script, while flow-driven packets glow as they ride the traces — both read as
/// electrical pulses. Set over a green-substrate-leaning dark board with copper/gold
/// accents (all token-indirected) and mono labels for the technical aesthetic. Derived from
/// <see cref="BeckStyle.Classic"/> with a <c>with</c> expression, so every feature (all shapes/variants,
/// groups, icons, edges + labels + UML markers, packets + labels, trails, highlight/pulse/fail, status
/// pills, narration, impact/working rings, sequence choreography, state/class diagrams, scrub, reduced
/// motion, light/dark) stays fully available — only tokens, geometry, typography, and the shape family
/// change.
/// </summary>
/// <remarks>
/// <para><b>Artwork (StyleArtwork.Circuit).</b> Pins are emitted behind each card/pill/class node by the
/// shared <c>Artwork.Rect</c> seam (a gated data selector, not injected markup): a deterministic ladder
/// whose count derives from the node's own height, filled through <c>--beck-pin</c> so only the outer
/// sliver shows past the opaque chip fill. Group boxes (fill:none), ghosts (dashed), and start/end
/// pseudo-states are deliberately left bare — pins on a hollow shape read as noise. Via dots are emitted
/// by the <em>edge</em> seam at each genuine bend of the route polyline (read from the SVG layer,
/// <c>Route/</c> untouched), filled through <c>--beck-via</c>; the edge stays one continuous
/// <c>&lt;path&gt;</c>, so packets/trails (which ride it via <c>offset-path</c>) and routing are
/// unchanged.</para>
/// <para><b>Pulse packets.</b> The travelling packet glyph keeps the classic dot, but the packet bloom
/// (<see cref="StyleMotion.GlowEnabled"/> with a slightly wider <see cref="StyleMotion.PacketGlowBlur"/>)
/// reads as a bright electrical pulse riding the trace's <c>offset-path</c> — no new motion mechanism,
/// the existing hash-scoped glow filter doing the work. Card sizing is unchanged: <c>NodeStroke</c> stays
/// 1.5 so <c>MeasureBorder</c> is classic's 2 and every box measures identically; only the drawn chrome
/// and colours change.</para>
/// <para><b>Amber signal comet (edge seam).</b> Beyond the flow-driven glow packets, every trace carries an
/// always-on <see cref="EdgeOverlay.Comet"/> — an 8px gold dot (<see cref="StyleEdges.CometDash"/>) riding
/// the full trace on a 2.2s compiled shared-cycle loop (<see cref="StyleEdges.OverlayPeriod"/>), width 3,
/// round cap, hue via the palette-less <c>--beck-edge-overlay</c> fallback (= <c>--beck-gold</c>). It is an
/// additional path sharing each trace's exact <c>d</c> (routing + the single continuous flow path
/// untouched), its per-edge phase baked into the start dash-offset (no delay chain), killed under reduced
/// motion. This is the mock's headline "signals pulse along right-angle traces" identity — flow-independent
/// and continuous, where the glow packets only fire when a flow script drives them.</para>
/// </remarks>
public static class CircuitStyle
{
    public static readonly BeckStyle Instance = Build();

    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static BeckStyle Build()
    {
        BeckStyle c = BeckStyle.Classic;
        const string copper = "var(--color-primary-600, #b87333)";
        const string gold = "#e0b34d";

        // Mono everything (like terminal): the single CSS token indirection point
        // (Stylesheet's scope{font-family:var(--beck-font);}) flips every default <text> to mono, while
        // the spots already on --beck-font-mono are unchanged. MetricsFont stays Inter — the textLength
        // guard absorbs the sans-metric vs mono-render gap, same tradeoff terminal makes.
        StyleTypography typography = c.Typography with { SansFamily = c.Typography.MonoFamily };

        // Light board: a pale green substrate with copper traces and gold vias/pins. Every entry keeps
        // the three-tier var(--beck-X, var(--color-Y, literal)) chain so a host palette still wins; only
        // the literal fallbacks lean PCB. --beck-pin / --beck-via are extra circuit tokens (like extrude's
        // --beck-depth*) feeding the StyleArtwork.Circuit chip pins + trace vias.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #eaf2ec)"),
            ("--beck-node-bg", "var(--color-base-50, #f6f2e9)"),
            ("--beck-node-border", $"color-mix(in srgb, {copper} 45%, var(--color-base-200, #cdd8cf))"),
            ("--beck-node-shadow", "0 1px 3px rgb(20 60 40 / 0.06), 0 4px 12px rgb(20 60 40 / 0.07)"),
            ("--beck-text", "var(--color-base-800, #163a29)"),
            ("--beck-text-muted", "var(--color-base-500, #4c6b58)"),
            ("--beck-text-faint", "var(--color-base-400, #7d9a88)"),
            ("--beck-primary", copper),
            ("--beck-success", "var(--color-emerald-500, #10b981)"),
            ("--beck-warn", "var(--color-amber-500, #f59e0b)"),
            ("--beck-danger", "var(--color-red-500, #ef4444)"),
            ("--beck-info", "var(--color-violet-500, #8b5cf6)"),
            ("--beck-neutral", $"color-mix(in srgb, {copper} 24%, var(--color-base-400, #7d9a88))"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {P(c.Mix.GroupBorder)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text-muted)"),
            ("--beck-edge", copper),
            ("--beck-packet", $"var(--beck-gold, {gold})"),
            ("--beck-icon-bg", $"color-mix(in srgb, {copper} 12%, var(--color-base-100, #e3ede5))"),
            ("--beck-accent", copper),
            // Circuit chrome tokens: copper chip pins, gold trace vias, and the dark trace bed under the
            // copper trace (the two-layer trace's wide darker layer — a darker mix of the copper edge).
            ("--beck-gold", $"var(--color-amber-400, {gold})"),
            ("--beck-pin", $"color-mix(in srgb, {copper} 80%, var(--beck-gold))"),
            ("--beck-via", "var(--beck-gold)"),
            ("--beck-edge-underlay", $"color-mix(in srgb, {copper} 60%, var(--color-base-800, #163a29))"),
            // The amber signal comet's hue (mock 1h's #fcd34d pulse) — the palette-less overlay fallback
            // token. Reuses --beck-gold so the travelling signal, the vias and the chip pins read as one
            // gold electrical family; theme-adapts through --beck-gold (dark inherits it unchanged).
            ("--beck-edge-overlay", "var(--beck-gold)"),
        });

        // Dark board (the circuit hero): a deep green substrate, near-black chip bodies with copper
        // borders, bright gold traces/vias. Partial override layered over the light block (emitted first).
        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #07231a)"),
            ("--beck-node-bg", "var(--color-base-900, #0d3527)"),
            ("--beck-node-border", $"color-mix(in srgb, {copper} 55%, var(--color-base-700, #1c5240))"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.4), 0 4px 14px rgb(0 0 0 / 0.5)"),
            ("--beck-text", "var(--color-base-50, #e8f5ee)"),
            ("--beck-text-muted", "var(--color-base-400, #8fb6a1)"),
            ("--beck-text-faint", "var(--color-base-500, #5f8570)"),
            ("--beck-edge", $"color-mix(in srgb, {copper} 60%, var(--beck-gold))"),
            ("--beck-icon-bg", "var(--color-base-800, #0a2d21)"),
            // Deep-emerald trace bed on the dark board, under the bright gold-copper trace.
            ("--beck-edge-underlay", $"color-mix(in srgb, {copper} 45%, var(--color-base-900, #052015))"),
        });

        // Chip-like geometry: small rounding (a socketed IC), plus the circuit artwork knobs — pin
        // length/thickness/pitch and via radius. NodeStroke stays 1.5 (classic MeasureBorder) so boxes
        // measure identically; only the copper trace stroke thickens a touch.
        StyleGeometry geo = c.Geometry with
        {
            CardRadius = 5,
            ClassRadius = 5,
            GhostRadius = 6,
            GroupRadius = 8,
            IconChipRadius = 4,
            GroupLabelBgRadius = 2,
            NarrationRadius = 5,
            BandRadius = 6,

            EdgeStroke = 1.8,

            PinLength = 6,
            PinThickness = 2.4,
            PinPitch = 24,
            ViaRadius = 2.6,
        };

        // Packets bloom brighter (the electrical-pulse read) — a wider glow blur over the classic dot.
        // GlowEnabled stays on; nothing else about the motion changes.
        StyleMotion motion = c.Motion with
        {
            PacketGlowBlur = 4.0,
            // The status LED (mock 1h): a small amber dot inset in the chip's top-right corner blinks
            // once as the signal lands — --beck-gold, the same hue as the ambient signal comet.
            // No lift/zoom — chips are soldered down; only the LED reacts.
            Pulse = PulseEffect.Led,
            PulseColor = "var(--beck-gold)",
            LiftEnabled = false,
        };

        // Circuit's signature two-layer trace + the amber signal comet (mock 1h's headline motion):
        //  - UnderlayWidth/Color: a static, wider, darker trace-bed underlay behind the copper base edge
        //    (sharing its exact d), so the thin copper line reads as a trace riding a dark bed. Beds
        //    architecture/class edges + sequence messages + lifelines; the base edge stays the one
        //    continuous flow path packets/trails ride. ~2.2× the 1.8 EdgeStroke.
        //  - Overlay=Comet: an amber signal pulse riding EVERY trace continuously, independent of any flow
        //    script — the ambient "signals pulse along right-angle traces" identity. Maps verbatim to the
        //    mock's `stroke:#fcd34d;stroke-width:3;stroke-linecap:round;stroke-dasharray:8 304;
        //    animation:pt 2.2s linear infinite`: an 8px gold dot (CometDash=8) over the full-length gap,
        //    width 3, round cap, on a 2.2s compiled shared-cycle loop. Its hue is the palette-less
        //    --beck-edge-overlay fallback (= --beck-gold); its per-edge phase is baked into the start
        //    dash-offset (no delay chain) and it is killed under reduced motion. An additional path
        //    sharing the edge's exact d — routing and the single continuous flow path are unchanged.
        StyleEdges edges = c.Edges with
        {
            UnderlayWidth = 4,
            UnderlayColor = "var(--beck-edge-underlay)",
            Overlay = EdgeOverlay.Comet,
            OverlayWidth = 3,
            CometDash = 8,
            OverlayPeriod = 2.2,
        };

        return c with
        {
            Name = "circuit",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Typography = typography,
            Motion = motion,
            Edges = edges,
            Artwork = StyleArtwork.Circuit,
        };
    }
}
