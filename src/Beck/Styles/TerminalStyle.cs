namespace Beck;

/// <summary>
/// The <c>terminal</c> built-in style (mock 1f): mono everything — every text role renders through the
/// mono family stack, block/square travelling packets that HOP in hard steps, a hard-step
/// (<c>steps(n)</c>) trail reveal, and a green-ramp default accent (with neutrals biased toward the same
/// success ramp) instead of classic's blue. Squared-off corners (radius 0 throughout) reinforce the
/// "console window" read, and — the headline edge trait, rebuilt around the edge-presentation seam
/// (<see cref="StyleEdges"/>) — a bright phosphor block <em>ticks down every wire</em> in hard discrete
/// jumps (<see cref="EdgeOverlay.Comet"/> + <see cref="StyleEdges.OverlaySteps"/>) over a dim green trace,
/// with mono <c>&gt;</c> chevron arrowheads sitting bright over it. Derived from <see cref="BeckStyle.Classic"/> with a <c>with</c> expression so every
/// feature (shapes, groups, icons, packets, trails, sequence choreography, state/class diagrams,
/// scrub, reduced motion, light/dark) stays fully available — only the rendering changes.
/// </summary>
/// <remarks>
/// The <c>[bracketed]</c> node-title affordance is data-driven through
/// <see cref="StyleTypography.TitlePrefix"/>/<see cref="StyleTypography.TitleSuffix"/>: the
/// style-scoped measurement seam decorates the title <em>before</em> <c>CardSizer</c> sizes the box
/// <em>and</em> before the renderer draws/word-wraps it, so the brackets add width to the card without
/// desyncing the <c>textLength</c> guard — the "measured widths guard the typography" invariant is
/// upheld, not risked. Applied to every primary node title (card/pill/class/ghost); subtitles, status
/// pills, and labels stay bare. The <em>edge</em> presentation is the star: the mono <c>&gt;</c> chevron
/// arrowhead (<see cref="EdgeArrow.Chevron"/> on <see cref="StyleEdges.Arrow"/>) — two hard butt-capped
/// strokes forming a crisp <c>&gt;</c> emitted through the <c>Markers</c> pipeline, oriented along the edge
/// so a reply reads as <c>&lt;</c> for free and drawn bright over the wire via
/// <see cref="StyleEdges.MarkerColor"/> — plus a bright phosphor block (<see cref="EdgeOverlay.Comet"/>,
/// <see cref="StyleEdges.OverlayWidth"/> 5 / <see cref="StyleEdges.CometDash"/> 5) that ticks the dim green
/// trace in twelve hard discrete jumps (<see cref="StyleEdges.OverlaySteps"/> = 12) each
/// <see cref="StyleEdges.OverlayPeriod"/>, compiled onto the shared cycle by
/// <c>CssCompiler.EdgeOverlayCss</c> (no delay chain) and killed under reduced motion; the travelling flow
/// packet hops in the same cadence (<see cref="StyleMotion.PacketSteps"/> = 12). Deliberately <em>not</em>
/// shipped: scanlines and a blinking cursor, both explicitly excluded by the design brief — the identity is
/// carried by mono type, square packets, hard-step trails/packet, the brackets, the chevron heads, and the
/// stepped phosphor pulse instead (no <c>scan</c>/<c>blink</c> keyframes).
/// </remarks>
public static class TerminalStyle
{
    public static readonly BeckStyle Instance = Build();

    private static BeckStyle Build()
    {
        BeckStyle c = BeckStyle.Classic;
        const string green = "var(--color-success-500, #4ade80)";

        // Every role renders through the same family stack as --beck-font-mono, so the handful of
        // spots that already opt into var(--beck-font-mono) (class members, msg/band text, packet
        // labels) stay exactly as they render today, while the *default* --beck-font — inherited by
        // every other <text> — becomes mono too. No per-role markup branch: this is the single CSS
        // token indirection point (Stylesheet.cs's `scope{font-family:var(--beck-font);}`) doing the
        // whole identity in one move.
        var typography = c.Typography with
        {
            SansFamily = c.Typography.MonoFamily,
            // The [bracketed] label affordance: every primary node title (card/pill/class/ghost) renders
            // as "[Title]". Applied via StyleTypography.DecorateTitle at both the measurement boundary
            // (CardSizer sizes the box for the bracketed run) and the render boundary (the same bracketed
            // run is drawn + word-wrapped), so the brackets widen the card and the textLength guard stays
            // matched — no desync. Subtitles/status/labels stay bare.
            TitlePrefix = "[",
            TitleSuffix = "]",
        };

        // --beck-accent is an explicit green default (not classic's alias to --beck-primary), and
        // --beck-packet follows it so packets read green by default too. --beck-neutral biases
        // toward the same success ramp via color-mix — a remapped ratio over the host ramp, still
        // fully overridable by a host --color-success-500 or --color-base-400.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #ffffff)"),
            ("--beck-node-bg", "var(--color-base-50, #ffffff)"),
            ("--beck-node-border", "var(--color-base-200, #e2e8f0)"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.05), 0 4px 12px rgb(0 0 0 / 0.06)"),
            ("--beck-text", "var(--color-base-800, #1e293b)"),
            ("--beck-text-muted", "var(--color-base-500, #64748b)"),
            ("--beck-text-faint", "var(--color-base-400, #94a3b8)"),
            ("--beck-primary", "var(--color-primary-600, #175ddc)"),
            ("--beck-success", "var(--color-emerald-500, #10b981)"),
            ("--beck-warn", "var(--color-amber-500, #f59e0b)"),
            ("--beck-danger", "var(--color-red-500, #ef4444)"),
            ("--beck-info", "var(--color-violet-500, #8b5cf6)"),
            ("--beck-neutral", $"color-mix(in srgb, {green} 30%, var(--color-base-400, #94a3b8))"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {c.Mix.GroupBorder.ToString(System.Globalization.CultureInfo.InvariantCulture)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text-muted)"),
            // The wire itself reads as a dim green trace (the mock's dark-green `#166534` connectors),
            // NOT classic's neutral slate — so the whole diagram is a coherent monochrome-green console.
            // The bright phosphor block (--beck-edge-overlay) then tick-travels this dim rail, and the
            // chevron heads (MarkerColor = accent) sit bright over it, exactly as the mock layers them.
            ("--beck-edge", "var(--color-success-800, #166534)"),
            // The travelling phosphor block's hue: a step BRIGHTER than the accent (mock's `#86efac`
            // green-300 block over its `#4ade80` nodes/heads), so the packet glows down the wire. This
            // is the palette-less overlay's fallback token (var(--beck-edge-overlay, var(--beck-accent))),
            // shared by both themes — phosphor stays vivid on light and dark alike.
            ("--beck-edge-overlay", "var(--color-success-300, #86efac)"),
            ("--beck-packet", "var(--beck-accent)"),
            ("--beck-icon-bg", $"color-mix(in srgb, {green} 12%, var(--color-base-100, #f1f5f9))"),
            ("--beck-accent", green),
        });

        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #0d1117)"),
            ("--beck-node-bg", "var(--color-base-900, #161b22)"),
            ("--beck-node-border", $"color-mix(in srgb, {green} 18%, var(--color-base-700, #30363d))"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.3), 0 4px 14px rgb(0 0 0 / 0.4)"),
            ("--beck-text", "var(--color-base-50, #f0f6fc)"),
            ("--beck-text-muted", "var(--color-base-400, #8b949e)"),
            ("--beck-text-faint", "var(--color-base-500, #6e7681)"),
            // --beck-edge is intentionally NOT overridden here: the dim green trace (success-800) tracks
            // through from the light block so the wire reads as the mock's dark-green connector on the
            // near-black console surface too (the mock is itself a dark page). --beck-edge-overlay
            // (phosphor green-300) likewise inherits — it stays bright on both themes.
            ("--beck-icon-bg", $"color-mix(in srgb, {green} 10%, var(--color-base-800, #21262d))"),
        });

        // Squared-off corners throughout — the "console window" read (StyleArtwork territory would
        // add real chrome; this stays a data-only radius change).
        StyleGeometry geo = c.Geometry with
        {
            CardRadius = 0,
            ClassRadius = 0,
            GhostRadius = 0,
            GroupRadius = 0,
            IconChipRadius = 0,
            GroupLabelBgRadius = 0,
            NarrationRadius = 0,
            BandRadius = 0,
            // The mono caption sets wider than classic's sans, so classic's 9.6px bullet→text gap
            // reads as jammed ("•user clicks buy"). Give the bullet more air for the terminal look.
            NarrationBulletGap = 14,
        };

        // The terminal edge presentation — the identity the mock carries on every wire (§1f):
        //  - Arrow=Chevron: mono `>` arrowheads (TWO hard butt-capped strokes via Markers.Body), oriented
        //    along the edge so a reply's reversed path draws `<` for free (orient="auto-start-reverse").
        //    Closed UML ends stay closed (the class inheritance triangle keeps its body).
        //  - MarkerColor=accent: the mock draws the `>` in bright green (`#4ade80`) OVER the dim green wire
        //    (`#166534`), not in the wire's own dim hue. MarkerColor only recolours edges still on the
        //    default var(--beck-edge) (an author-coloured edge keeps its colour + matching head), so the
        //    dim rail carries a bright phosphor chevron exactly as the mock layers them.
        //  - Overlay=Comet + OverlaySteps=12: the headline "packets ticking down the wires" — a bright
        //    phosphor block (OverlayWidth 5, CometDash 5 → the mock's `stroke-width:5;stroke-dasharray:5 298`)
        //    that TICKS the wire in 12 hard discrete jumps (`animation:ptd 1.6s steps(12) infinite`) rather
        //    than gliding. OverlaySteps is the one seam field this needs (shared with brutalist); it swaps
        //    the overlay's linear timing for steps(n), extending the existing stepped-flow discipline
        //    (PacketSteps/TrailSteps) to the ambient edge overlay. Compiled onto the shared cycle by
        //    CssCompiler.EdgeOverlayCss (no delay chain), killed under reduced motion. The block hue is the
        //    palette-less fallback token var(--beck-edge-overlay) = phosphor green-300 (set above).
        //  - OverlayLinecap=butt + BaseLinecap=butt: the phosphor is a hard BLOCK, and the base wire is
        //    squared (the mock lines carry no linecap) — matching the style's squared-corner console chassis.
        // The mock's scanlines + blinking cursor stay LOCKED OUT (design brief): the identity is carried by
        // mono type, square packets, hard-step trails/packet, brackets, chevrons, and this stepped phosphor
        // pulse — no scan/blink keyframes.
        StyleEdges edges = c.Edges with
        {
            Arrow = EdgeArrow.Chevron,
            MarkerColor = "var(--beck-accent)",
            BaseLinecap = "butt",
            Overlay = EdgeOverlay.Comet,
            OverlayWidth = 5,
            OverlayLinecap = "butt",
            CometDash = 5,
            OverlaySteps = 12,
            OverlayPeriod = 1.6,
        };

        StyleMotion motion = c.Motion with
        {
            // Block/square travelling packets (the "packet emitter" seam: CssCompiler.Markup swaps
            // <circle> for a centred <rect> when a hop resolves to this shape).
            PacketGlyph = PacketGlyph.Square,
            // Hard-step trail reveal — a blocky steps(n) timing function on the trail's
            // stroke-dashoffset track instead of the packet's own (still-smooth) ease.
            TrailSteps = 8,
            // The travelling flow packet HOPS its edge in discrete jumps too (the mock steps the flow —
            // its `ptd` block ticks, it never glides): a steps(12) timing on the packet's own
            // offset-distance track, matching the stepped phosphor overlay's cadence. Only the flow
            // effect steps — nothing animates at rest.
            PacketSteps = 12,
            // No bloom — crisp edges are part of the "no scanlines, no cursor" restraint.
            GlowEnabled = false,
            // The CRT blink (mock 1f's `crt`): a card receiving a packet invert-flickers twice —
            // two instant-on/off fill flashes, phosphor-style, matching the stepped packets above.
            // No lift/zoom — a phosphor cell doesn't move, it flashes.
            Pulse = PulseEffect.Flicker,
            LiftEnabled = false,
        };

        return c with
        {
            Name = "terminal",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Typography = typography,
            Motion = motion,
            Edges = edges,
        };
    }
}
