using Beck.Rendering.Text;

namespace Beck;

/// <summary>
/// The <c>extrude</c> built-in style (Phase 4, artwork): playful 2.5D slabs. Every card/pill/class
/// node keeps its straight rounded rect (so all token-driven fill/stroke/filter still applies) but
/// gains two solid <em>depth faces</em> — a right and a bottom parallelogram — drawn behind it and
/// offset down-right as if lit from the top-left (<see cref="StyleArtwork.Extruded"/>), giving each
/// node an apparent thickness. Depth is <em>static</em> — nothing bobs at rest; the identity motion
/// is a <em>press-down</em> (<see cref="StyleMotion.PressDown"/>): a node's pulse/highlight presses
/// toward its faces (<c>translate(2px,2px)</c>) instead of the classic lift, so the slab reads as
/// pushed into the page. Slightly-saturated indigo surfaces and weightier edges/packets complete the
/// toy-brick read. Derived from <see cref="BeckStyle.Classic"/> with a <c>with</c> expression, so
/// every feature (all shapes/variants, groups, icons, edges + labels + UML markers, packets + labels,
/// trails, highlight/pulse/fail, status pills, narration, impact/working rings, sequence choreography,
/// state/class diagrams, scrub, reduced motion, light/dark) stays fully available — only tokens,
/// geometry, motion, and the shape family change.
/// </summary>
/// <remarks>
/// <para><b>Artwork (StyleArtwork.Extruded).</b> The two faces are emitted behind each card/pill/class
/// node by the <c>Artwork.Rect</c> shape seam (a gated data selector, not injected markup) — filled
/// through <c>--beck-depth-right</c> / <c>--beck-depth-bottom</c> tokens (a darker <c>color-mix</c> of
/// the node surface, so accented nodes get colour-matched depth and both themes adapt) and carrying no
/// stroke, so only the down-right depth sliver shows past the node's opaque fill. The router is
/// untouched; group boxes (fill:none), ghosts (dashed), and start/end pseudo-states are deliberately
/// left flat — a solid slab behind a hollow shape reads as noise.</para>
/// <para><b>Press-down, not bob.</b> The depth is baked geometry, never animated. On a pulse/highlight
/// the whole node group (rect + its faces, both inside <c>.beck-fx-node</c>) presses down-right by 2px
/// toward the base and settles back, compiled into the same shared-cycle transform keyframes as classic
/// — no <c>animation-delay</c>, and reduced-motion users get the fully-revealed static slab. Card
/// sizing is unchanged: <c>NodeStroke = 2</c> keeps <c>MeasureBorder</c> at 2 (classic's budget), so
/// the boxes measure identically and only the drawn chrome thickens.</para>
/// <para><b>Magenta comet (StyleEdges).</b> The mock's headline edge trait — a violet base rail
/// (<c>--beck-edge</c> saturated toward <c>#6d28d9</c>/<c>#8b5cf6</c>) under a magenta comet
/// (<see cref="EdgeOverlay.Comet"/>, <c>--beck-comet</c>, width 4, a 2px lit dash gliding every
/// edge/message continuously on a ~2.6s compiled shared-cycle loop, phased per edge from the content
/// hash) — rides on top, every card/pill/class node still static except for the press. The mock's
/// <c>floaty</c> node bob (nodes bobbing on offset phases) is explicitly locked out; press-down above
/// is extrude's only node motion.</para>
/// </remarks>
public static class ExtrudeStyle
{
    public static readonly BeckStyle Instance = Build();

    private static BeckStyle Build()
    {
        BeckStyle c = BeckStyle.Classic;

        // Slightly-saturated indigo token table. Every entry keeps the three-tier
        // var(--beck-X, var(--color-Y, literal)) indirection, so a host --color-* / --beck-* palette
        // still wins; only the literal fallbacks warm to a playful lilac/indigo. The group-border entry
        // threads mix.GroupBorder so the 45% ratio has one source. The three --beck-depth* tokens feed
        // the StyleArtwork.Extruded faces: in light, --beck-depth is a dark ink and the right/bottom
        // faces are a color-mix of it over the node surface (so accented nodes — which override
        // --beck-node-bg inline — get colour-matched depth), the bottom face darker per the top-left
        // light. The dark block re-derives the faces from an *elevated* ink (see below) so they stay
        // visible against the near-black page instead of sinking into it.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #f4f3ff)"),
            ("--beck-node-bg", "var(--color-base-50, #ffffff)"),
            ("--beck-node-border", "var(--color-base-300, #cfd0e8)"),
            ("--beck-node-shadow", "0 1px 2px rgb(40 30 90 / 0.06), 0 3px 8px rgb(40 30 90 / 0.07)"),
            ("--beck-text", "var(--color-base-800, #2a2450)"),
            ("--beck-text-muted", "var(--color-base-500, #6b6494)"),
            ("--beck-text-faint", "var(--color-base-400, #9a93c0)"),
            ("--beck-primary", "var(--color-primary-600, #5b4be6)"),
            ("--beck-success", "var(--color-emerald-500, #10b981)"),
            ("--beck-warn", "var(--color-amber-500, #f59e0b)"),
            ("--beck-danger", "var(--color-red-500, #ef4444)"),
            ("--beck-info", "var(--color-violet-500, #8b5cf6)"),
            ("--beck-neutral", "var(--color-base-400, #9a93c0)"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {P(c.Mix.GroupBorder)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text-muted)"),
            // Saturated violet base rail (mock 1e's `#6d28d9` connectors) in place of a muted grey — the
            // comet overlay reads as a bright accent riding a colourful line, not a neutral one.
            ("--beck-edge", "var(--color-violet-700, #6d28d9)"),
            ("--beck-packet", "var(--beck-primary)"),
            ("--beck-icon-bg", "var(--color-base-100, #eceafc)"),
            ("--beck-accent", "var(--beck-primary)"),
            // 2.5D depth ink + the two face fills (a darker color-mix of the node surface).
            ("--beck-depth", "var(--color-base-500, #6b6494)"),
            ("--beck-depth-right", "color-mix(in srgb, var(--beck-depth) 38%, var(--beck-node-bg))"),
            ("--beck-depth-bottom", "color-mix(in srgb, var(--beck-depth) 58%, var(--beck-node-bg))"),
            // The magenta comet hue (mock's `#e879f9`) riding every edge/message — a single fuchsia
            // token, not a palette, since extrude's identity is one comet colour everywhere.
            ("--beck-comet", "var(--color-fuchsia-400, #e879f9)"),
        });

        // Dark overrides only (layered over the light block, which is emitted first): a deep indigo-black
        // page. Unlike light, the depth ink here is an *elevated* mid-indigo (not a near-black), and the
        // two faces re-derive with swapped ratios so the slab walls read as lifted edges catching ambient
        // light — a near-black face would sink to the page value and the extrude identity would vanish
        // (nodes read as classic-dark). Ordering is preserved across themes: the right face stays the
        // lighter/more-lit wall, the bottom face the darker/deeper one, only now both sit *above* the
        // surface instead of below it.
        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #0c0b16)"),
            ("--beck-node-bg", "var(--color-base-900, #171526)"),
            ("--beck-node-border", "var(--color-base-700, #35324f)"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.4), 0 4px 14px rgb(0 0 0 / 0.5)"),
            ("--beck-text", "var(--color-base-50, #f2effc)"),
            ("--beck-text-muted", "var(--color-base-400, #a79fcf)"),
            ("--beck-text-faint", "var(--color-base-500, #7a739e)"),
            // A lighter, still-saturated violet so the base rail contrasts the near-black dark surface
            // (the light table's #6d28d9 would sink into it).
            ("--beck-edge", "var(--color-violet-500, #8b5cf6)"),
            ("--beck-icon-bg", "var(--color-base-800, #221f36)"),
            // Elevated depth ink + faces lifted above the node surface (mix toward the ink, not toward
            // black), so the offset slab walls contrast the near-black page. Right = more lift (lit wall),
            // bottom = less lift (deeper wall) — same right>bottom brightness order as light.
            ("--beck-depth", "var(--color-base-600, #4a4570)"),
            ("--beck-depth-right", "color-mix(in srgb, var(--beck-depth) 62%, var(--beck-node-bg))"),
            ("--beck-depth-bottom", "color-mix(in srgb, var(--beck-depth) 40%, var(--beck-node-bg))"),
        });

        // Chunky-slab geometry: modest rounding, a thicker node/message stroke and weightier edges (the
        // "toy brick" read). NodeStroke = 2 keeps MeasureBorder at 2 (2·round(2/2)) — classic's budget —
        // so the measured box is unchanged and only the drawn stroke thickens. The node drop-shadow is
        // kept faint (a soft contact shadow under the slab); the depth itself is the StyleArtwork.Extruded
        // faces (DepthOffset below), not a CSS filter, so no resolved colour touches shape CSS.
        StyleGeometry geo = c.Geometry with
        {
            CardRadius = 10,
            ClassRadius = 9,
            GhostRadius = 12,
            GroupRadius = 14,
            IconChipRadius = 7,
            GroupLabelBgRadius = 3,
            NarrationRadius = 10,
            BandRadius = 12,

            NodeStroke = 2,
            EdgeStroke = 2.0,
            GroupStroke = 1.5,
            BandBoxStroke = 1.5,
            LifelineStroke = 2,
            EndNodeStroke = 2,
            HairlineStroke = 1,
            MessageStroke = 2.4,

            NodeShadow = "drop-shadow(0 2px 2px rgb(40 30 90/.10))",
            NodeShadowDark = "drop-shadow(0 2px 4px rgb(0 0 0/.45))",

            DepthOffset = 7,
        };

        // Weightier packets to match the chunky edges: a fatter ring stroke floor + factor. Everything
        // else (durations, dim ratios, glow) stays classic. PressDown swaps the pulse/highlight lift for
        // a down-right press toward the depth faces (extrude's identity motion) — compiled into the same
        // shared-cycle transform keyframes, nothing animates at rest.
        StyleMotion motion = c.Motion with
        {
            PressDown = true,
            PacketRingMin = 3.0,
            PacketRingFactor = 0.32,
        };

        // The magenta comet (mock 1e): a second path sharing every edge/message's exact d, a 2px lit dash
        // gliding the whole run every ~2.6s, compiled shared-cycle with a baked per-edge phase (no delay
        // chain), killed under reduced motion. This IS extrude's headline gap — the mock's `floaty` node
        // bob is locked out (PressDown above already carries the identity motion); the comet is the only
        // add. Single magenta hue via --beck-comet (no multi-hue palette — that's metro's trait, not
        // extrude's), width 4 to read as a weighty "toy-brick" comet over the chunky base rail.
        StyleEdges edges = c.Edges with
        {
            Overlay = EdgeOverlay.Comet,
            OverlayWidth = 4,
            OverlayLinecap = "round",
            CometDash = 2,
            OverlayPeriod = 2.6,
            OverlayPalette = new[] { "var(--beck-comet)" },
            // Solid lifelines (mock 1e draws them as plain `#3b0764` width-1.5 verticals, no dash) —
            // a dashed scaffold under the chunky slabs read as noise; solid rails match the toy-brick weight.
            Lifeline = LifelineShape.FaintSolid,
        };

        return c with
        {
            Name = "extrude",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Motion = motion,
            Edges = edges,
            // The 2.5D depth-face artwork seam: card/pill/class nodes gain two solid parallelogram faces
            // (DepthOffset px, --beck-depth-*) behind them. Data-only selector — no injected markup.
            Artwork = StyleArtwork.Extruded,
        };
    }

    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
