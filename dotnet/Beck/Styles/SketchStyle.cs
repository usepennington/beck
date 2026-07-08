using Beck.Rendering.Text;

namespace Beck;

/// <summary>
/// The <c>sketch</c> built-in style (Phase 4, artwork): a hand-drawn look — warm-paper tokens,
/// friendly rounded geometry, <em>Shantell Sans</em> throughout, and node/group/pseudo-state outlines
/// drawn as subtly-wobbly closed paths (<see cref="StyleArtwork.Sketch"/>). Derived from
/// <see cref="BeckStyle.Classic"/> with a <c>with</c> expression, so every feature (all shapes/variants,
/// groups, icons, edges + labels + UML markers, packets + labels, trails, highlight/pulse/fail, status
/// pills, narration, impact/working rings, sequence choreography, state/class diagrams, scrub, reduced
/// motion, light/dark) stays fully available — only tokens, geometry, typography, and the shape family
/// change.
/// </summary>
/// <remarks>
/// <para><b>Deterministic wobble, no continuous motion.</b> The jitter is baked into the outline path
/// geometry, seeded off the diagram's content hash + the node id (see <c>Artwork</c>), so the same YAML
/// wobbles the same way forever and nothing breathes/wobbles on a loop. The "arrows draw themselves on"
/// read is carried by the <em>existing</em> reveal choreography this style inherits unchanged — flow
/// trails already draw along each edge as the packet travels — not a new mechanism.</para>
/// <para><b>Nodes wobble, edges stay exact (deferred).</b> Only node rects/pills/class cards, group
/// boxes, and start/end circles become wobbly paths; router edge paths are left as their exact straight
/// geometry. Waving an edge would perturb the single-<c>&lt;path&gt;</c> / <c>offset-path</c> contract
/// that packets and trails ride, and re-deriving a wavy path from route geometry risks the
/// no-off-canvas-coordinates invariant — so edge wobble is intentionally out of scope here (new-designs.md
/// permits "wobble nodes only and note it"). Hand-drawn markers are likewise deferred: <c>Markers</c> is
/// keyed only by shape with no style dimension, and arrowheads carry no identity that the wobbly outlines
/// + Shantell type don't already deliver.</para>
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
        // holds. The group-border entry threads mix.GroupBorder so the 45% ratio has one source.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #fbf7ef)"),
            ("--beck-node-bg", "var(--color-base-50, #fffdf8)"),
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
            ("--beck-edge", "var(--color-base-300, #c9bda3)"),
            ("--beck-packet", "var(--beck-primary)"),
            ("--beck-icon-bg", "var(--color-base-100, #f3ecdd)"),
            ("--beck-accent", "var(--beck-primary)"),
        });

        // Dark overrides only (layered over the light block, which is emitted first): a warm near-black
        // paper with warm-brown ink/edges for the same hand-drawn feel on a dark page.
        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #1c1815)"),
            ("--beck-node-bg", "var(--color-base-900, #262019)"),
            ("--beck-node-border", "var(--color-base-700, #453b2e)"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.4), 0 4px 14px rgb(0 0 0 / 0.5)"),
            ("--beck-text", "var(--color-base-50, #f5efe2)"),
            ("--beck-text-muted", "var(--color-base-400, #b3a893)"),
            ("--beck-text-faint", "var(--color-base-500, #857a66)"),
            ("--beck-edge", "var(--color-base-700, #4a4030)"),
            ("--beck-icon-bg", "var(--color-base-800, #2f271d)"),
        });

        // Friendly rounded geometry + slightly heavier ink stroke (reads as a felt-tip outline). NodeStroke
        // 2 keeps MeasureBorder at 2 (2·round(2/2)) — identical to classic's budget — so the measured box is
        // unchanged and only the drawn stroke thickens. Soft warm drop-shadows instead of classic's cool
        // ones; shadow colours are rgb() literals (shadows are never themed tokens), no resolved colour
        // touches a shape fill/stroke (those stay on --beck-* tokens).
        StyleGeometry geo = c.Geometry with
        {
            CardRadius = 16,
            ClassRadius = 14,
            GhostRadius = 18,
            GroupRadius = 20,
            IconChipRadius = 10,
            GroupLabelBgRadius = 4,
            NarrationRadius = 14,
            BandRadius = 16,

            NodeStroke = 2,
            GroupStroke = 2,
            EdgeStroke = 1.8,
            BandBoxStroke = 1.6,
            LifelineStroke = 2,
            EndNodeStroke = 2,
            HairlineStroke = 1.2,
            MessageStroke = 2,

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

        return c with
        {
            Name = "sketch",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Typography = typography,
            Artwork = StyleArtwork.Sketch,
        };
    }

    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
