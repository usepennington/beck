using Beck.Rendering.Text;

namespace Beck;

/// <summary>
/// The <c>metro</c> built-in style (Phase 4, artwork, mock 1i): a transit map. Edges become thick,
/// round-capped <em>transit lines</em> and each line drops a white <em>station dot</em> at its two
/// anchor endpoints (<see cref="StyleArtwork.Metro"/>) — a filled circle whose ring takes the line's
/// own colour, drawn over the stroke. Packets ride the lines as elongated <em>train capsules</em>
/// (<see cref="Beck.PacketGlyph.Train"/>) that lean into the route with the path tangent. Headers are
/// set in <em>Archivo</em> with roomier letter-spacing for a clean station-sign read, over clean flat
/// surfaces and bright, saturated line-accent defaults. Derived from <see cref="BeckStyle.Classic"/>
/// with a <c>with</c> expression, so every feature (all shapes/variants, groups, icons, edges +
/// labels + UML markers, packets + labels, trails, highlight/pulse/fail, status pills, narration,
/// impact/working rings, sequence choreography, state/class diagrams, scrub, reduced motion,
/// light/dark) stays fully available — only tokens, geometry, typography, motion, and the shape
/// family change.
/// </summary>
/// <remarks>
/// <para><b>Artwork (StyleArtwork.Metro).</b> Station dots are emitted by the edge/message seams at
/// each edge's two anchor endpoints (read from the already-computed route geometry — the architecture
/// polyline's first/last point, the sequence message endpoints), filled through the surface token
/// (<c>var(--beck-station-fill, var(--beck-surface))</c>) so the "white station" theme-adapts and the
/// ring takes the edge's own colour so each transit line's stations match its hue. The router is
/// untouched and each edge stays one continuous <c>&lt;path&gt;</c>; the dots are additional sibling
/// elements drawn over it. Group boxes, ghosts, and pseudo-states carry no stations. NodeStroke stays
/// 1.5 (classic <c>MeasureBorder</c>) so every card box measures identically — only the drawn chrome,
/// the thick edge stroke, and the typography change.</para>
/// <para><b>Train packets.</b> The default packet glyph is <see cref="Beck.PacketGlyph.Train"/>: an
/// elongated rounded-rect capsule centred on the offset point with <c>offset-rotate:auto</c>, so it is
/// the one glyph that rotates with the path — a carriage leaning through curves. No new motion
/// mechanism; it rides the same <c>offset-path</c> track every packet uses. An author's explicit
/// <c>packet.shape</c> still overrides it (the glyph is only the style default).</para>
/// <para><b>Station-sign type.</b> The card / pill / class / diagram title roles gain roomier
/// letter-spacing sized against the embedded Archivo table (<see cref="MetricsFont.Archivo"/>), so the
/// boxes measure to the tracked run and the <c>textLength</c> guard keeps the drawn text inside its
/// box (the same measured-tracking mechanism classic already uses for the class stereotype).</para>
/// </remarks>
public static class MetroStyle
{
    public static readonly BeckStyle Instance = Build();

    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static BeckStyle Build()
    {
        BeckStyle c = BeckStyle.Classic;

        // Clean station-panel surfaces with bright, saturated transit-line accents. Every entry keeps
        // the three-tier var(--beck-X, var(--color-Y, literal)) indirection, so a host --color-* /
        // --beck-* palette still wins; only the literal fallbacks lean toward classic subway-line hues
        // (blue / district-green / orange / central-red / metropolitan-purple / jubilee-grey). The
        // group-border entry threads mix.GroupBorder so its 45% ratio has one source. Station dots need
        // no extra token — their white fill tracks --beck-surface through the Artwork.Station fallback.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #f7f7f4)"),
            ("--beck-node-bg", "var(--color-base-50, #ffffff)"),
            ("--beck-node-border", "var(--color-base-200, #e2e8f0)"),
            ("--beck-node-shadow", "0 1px 2px rgb(0 0 0 / 0.05), 0 3px 8px rgb(0 0 0 / 0.06)"),
            ("--beck-text", "var(--color-base-900, #16202b)"),
            ("--beck-text-muted", "var(--color-base-500, #5b6875)"),
            ("--beck-text-faint", "var(--color-base-400, #94a3b8)"),
            ("--beck-primary", "var(--color-primary-600, #0055cc)"),
            ("--beck-success", "var(--color-emerald-500, #00843d)"),
            ("--beck-warn", "var(--color-amber-500, #f58025)"),
            ("--beck-danger", "var(--color-red-500, #e1251b)"),
            ("--beck-info", "var(--color-violet-500, #6d3f97)"),
            ("--beck-neutral", "var(--color-base-400, #838d93)"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {P(c.Mix.GroupBorder)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text-muted)"),
            ("--beck-edge", "var(--color-base-400, #8a97a6)"),
            ("--beck-packet", "var(--beck-primary)"),
            ("--beck-icon-bg", "var(--color-base-100, #eef1f5)"),
            ("--beck-accent", "var(--beck-primary)"),
        });

        // Dark overrides only (layered over the light block, which is always emitted first): a deep
        // slate page with brighter blue lines and a stronger default-line grey so the thick transit
        // strokes stay legible on black. The saturated accent literals above already read on dark.
        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #10151b)"),
            ("--beck-node-bg", "var(--color-base-900, #1a2129)"),
            ("--beck-node-border", "var(--color-base-700, #30363d)"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.35), 0 4px 14px rgb(0 0 0 / 0.45)"),
            ("--beck-text", "var(--color-base-50, #f0f6fc)"),
            ("--beck-text-muted", "var(--color-base-400, #8b949e)"),
            ("--beck-text-faint", "var(--color-base-500, #6e7681)"),
            ("--beck-primary", "var(--color-primary-500, #3b82f6)"),
            ("--beck-edge", "var(--color-base-600, #566372)"),
            ("--beck-icon-bg", "var(--color-base-800, #21262d)"),
        });

        // Clean, gently-rounded station-panel surfaces + the thick transit-line stroke. Round line
        // caps/joins are already the .beck-edge default, so a thicker EdgeStroke alone makes the metro
        // line. NodeStroke stays 1.5 → MeasureBorder is classic's 2, so card boxes measure identically.
        // StationRadius/StationRing feed the StyleArtwork.Metro station dots (large enough that the
        // white fill shows past the 5px line).
        StyleGeometry geo = c.Geometry with
        {
            CardRadius = 12,
            ClassRadius = 10,
            GhostRadius = 14,
            GroupRadius = 16,
            IconChipRadius = 8,
            GroupLabelBgRadius = 3,
            NarrationRadius = 12,
            BandRadius = 14,

            EdgeStroke = 5,
            MessageStroke = 5,

            StationRadius = 4.5,
            StationRing = 2.5,
        };

        // Station-sign typography: Archivo headers with roomier letter-spacing. The card/pill/class
        // title roles gain tracking sized against the Archivo table (CardSizer measures through this
        // role table), so the boxes grow to the tracked run and the textLength guard keeps the drawn
        // text inside them — the same measured-tracking mechanism classic already relies on for the
        // class stereotype. The diagram title emits its letter-spacing directly (its own render path),
        // so it tracks crisply. Weights stay classic (titles are already 600 semibold).
        StyleTypography typography = c.Typography with
        {
            SansFamily = "'Archivo', system-ui, -apple-system, sans-serif",
            MetricsFont = MetricsFont.Archivo,
            Roles = new FontRoleTable(role => role switch
            {
                FontRole.CardTitle => FontRoles.Of(role) with { LetterSpacingEm = 0.02 },
                FontRole.PillTitle => FontRoles.Of(role) with { LetterSpacingEm = 0.02 },
                FontRole.ClassTitle => FontRoles.Of(role) with { LetterSpacingEm = 0.02 },
                FontRole.DiagramTitle => FontRoles.Of(role) with { LetterSpacingEm = 0.01 },
                _ => FontRoles.Of(role),
            }),
        };

        // Train-capsule packets: the style default glyph, an elongated rounded-rect that rotates with
        // the path (offset-rotate:auto). Nothing else about the motion changes; author packet.shape
        // still overrides. The classic per-edge-kind glow still blooms the capsule.
        StyleMotion motion = c.Motion with
        {
            PacketGlyph = PacketGlyph.Train,
        };

        return c with
        {
            Name = "metro",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Typography = typography,
            Motion = motion,
            // The station-dot artwork seam: each edge drops a white-fill, line-coloured-ring circle at
            // both anchor endpoints. Data-only selector — no injected markup, router untouched.
            Artwork = StyleArtwork.Metro,
        };
    }
}
