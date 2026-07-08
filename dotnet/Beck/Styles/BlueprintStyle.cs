using Beck.Rendering.Text;

namespace Beck;

/// <summary>
/// The <c>blueprint</c> built-in style (Phase 3, mock 1a): a technical-drawing look — a faint
/// token-driven graph-paper grid painted on the diagram surface, blue-leaning token defaults over the
/// host ramp, dashed "flowing" edges (every edge carries the style's edge dash, and the flow's
/// marching stream overlay rides on top), and mono, uppercase annotation labels. Derived from
/// <see cref="BeckStyle.Classic"/> with a <c>with</c> expression so every feature (shapes, groups,
/// icons, packets, trails, sequence choreography, state/class diagrams, scrub, reduced motion,
/// light/dark) stays fully available by construction — only the surface, edges, tokens, and label
/// typography change.
/// </summary>
/// <remarks>
/// <para>The grid is a CSS <c>background-image</c> (two token-coloured gradients) on the root
/// <c>&lt;svg&gt;</c> box, driven by a new <c>--beck-grid</c> token defined in both the light and dark
/// tables, so it theme-adapts and a host <c>--color-*</c>/<c>--beck-*</c> override still wins. Edge
/// dashing rides the existing single continuous <c>&lt;path&gt;</c> per edge (a stroke treatment
/// only), so routing/packets/trails are untouched.</para>
/// <para><em>Dimension ticks on group boxes</em> ship via the <see cref="StyleArtwork.Blueprint"/>
/// branch in the group-box painter (a subtle top-edge dimension line + perpendicular witness ticks,
/// token-coloured through <c>--beck-dimension</c>, gated by <see cref="StyleGeometry.DimensionTick"/>).
/// Still deferred: <em>uppercase node titles</em> — uppercasing titles/subtitles is safe for
/// <em>measurement</em> now (the style's <see cref="FontRoles"/> table feeds <c>CardSizer</c>), but
/// blueprint keeps titles mixed-case as a deliberate design choice (the mono-uppercase treatment is
/// reserved for the annotation label roles — edge/group/band/message labels — where it reads as
/// draughting callouts rather than shouting every card). Edge labels carry a <c>textLength</c> guard;
/// group labels are already uppercased at the render site.</para>
/// </remarks>
public static class BlueprintStyle
{
    public static readonly BeckStyle Instance = Build();

    private static BeckStyle Build()
    {
        BeckStyle c = BeckStyle.Classic;

        // Blue-leaning defaults over the host ramp: primary/accent bias to the blue ramp, edges take a
        // blue tint via color-mix, and a faint --beck-grid token carries the graph-paper lines. Every
        // entry keeps the three-tier var(--beck-X, var(--color-Y, literal)) indirection, so a site that
        // defines --color-* (or --beck-* directly) always wins — the literals are only the last resort.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #ffffff)"),
            ("--beck-node-bg", "var(--color-base-50, #ffffff)"),
            ("--beck-node-border", "color-mix(in srgb, var(--beck-primary) 22%, var(--color-base-200, #e2e8f0))"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.05), 0 4px 12px rgb(0 0 0 / 0.06)"),
            ("--beck-text", "var(--color-base-800, #1e293b)"),
            ("--beck-text-muted", "var(--color-base-500, #64748b)"),
            ("--beck-text-faint", "var(--color-base-400, #94a3b8)"),
            ("--beck-primary", "var(--color-primary-600, #2563eb)"),
            ("--beck-success", "var(--color-emerald-500, #10b981)"),
            ("--beck-warn", "var(--color-amber-500, #f59e0b)"),
            ("--beck-danger", "var(--color-red-500, #ef4444)"),
            ("--beck-info", "var(--color-violet-500, #8b5cf6)"),
            ("--beck-neutral", "color-mix(in srgb, var(--beck-primary) 24%, var(--color-base-400, #94a3b8))"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {P(c.Mix.GroupBorder)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text-muted)"),
            ("--beck-edge", "color-mix(in srgb, var(--beck-primary) 42%, var(--color-base-300, #cbd5e1))"),
            ("--beck-packet", "var(--beck-primary)"),
            ("--beck-icon-bg", "color-mix(in srgb, var(--beck-primary) 8%, var(--color-base-100, #f1f5f9))"),
            ("--beck-accent", "var(--beck-primary)"),
            // Faint graph-paper line colour — kept low so the grid reads as a surface, not chrome.
            ("--beck-grid", "color-mix(in srgb, var(--beck-primary) 12%, transparent)"),
            // Dimension-line colour — a touch stronger than the grid so the group ticks read as an
            // annotation, but still a subtle primary tint (no resolved literal in shape CSS).
            ("--beck-dimension", "color-mix(in srgb, var(--beck-primary) 32%, transparent)"),
        });

        // Dark overrides only (layered over the light block, which is always emitted first): lighter
        // blue grid lines for contrast on the dark surface, plus the usual dark neutrals.
        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #0d1117)"),
            ("--beck-node-bg", "var(--color-base-900, #161b22)"),
            ("--beck-node-border", "color-mix(in srgb, var(--beck-primary) 26%, var(--color-base-700, #30363d))"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.3), 0 4px 14px rgb(0 0 0 / 0.4)"),
            ("--beck-text", "var(--color-base-50, #f0f6fc)"),
            ("--beck-text-muted", "var(--color-base-400, #8b949e)"),
            ("--beck-text-faint", "var(--color-base-500, #6e7681)"),
            ("--beck-edge", "color-mix(in srgb, var(--beck-primary) 46%, var(--color-base-700, #30363d))"),
            ("--beck-icon-bg", "color-mix(in srgb, var(--beck-primary) 14%, var(--color-base-800, #21262d))"),
            ("--beck-grid", "color-mix(in srgb, var(--color-primary-400, #60a5fa) 15%, transparent)"),
            ("--beck-dimension", "color-mix(in srgb, var(--color-primary-400, #60a5fa) 34%, transparent)"),
        });

        // The faint graph-paper grid: two token-coloured 1px gradients (horizontal + vertical rules) on
        // a 22px pitch, painted on the root <svg> box. Colour flows through --beck-grid so it
        // theme-adapts; no resolved literal touches shape CSS.
        const string grid =
            "background-image:linear-gradient(var(--beck-grid) 1px, transparent 1px)," +
            "linear-gradient(90deg, var(--beck-grid) 1px, transparent 1px);" +
            "background-size:22px 22px;";

        StyleGeometry geo = c.Geometry with
        {
            // Squarer corners than classic read as drafted/technical (still a data-only radius change).
            CardRadius = 4,
            ClassRadius = 4,
            GhostRadius = 4,
            GroupRadius = 4,
            IconChipRadius = 3,
            GroupLabelBgRadius = 2,
            NarrationRadius = 4,
            BandRadius = 4,
            SurfaceBackground = grid,
            // Dimension ticks on group boxes (the StyleArtwork.Blueprint gate below reads this): the
            // dimension rule sits 9px above each group's top edge with 3px witness overshoot.
            DimensionTick = 9,
        };

        StyleStrokes strokes = c.Strokes with
        {
            // Every edge draws dashed by default (the technical-drawing identity); a longer dash than
            // classic's authored-dashed pattern so the flowing stream overlay reads distinctly over it.
            DashedEdges = true,
            EdgeDash = "6 4",
        };

        // Mono + uppercase annotation labels. Only the family/case flags change; sizes/weights/spacing
        // stay classic so measured widths (which the embedded measurer computes from the static role
        // table) stay put. Edge labels are textLength-guarded, and group labels are already uppercased
        // at the render site, so this is layout-safe. Node titles stay sans (see the remarks note).
        //
        // MsgText (sequence message labels) joins the uppercase treatment so blueprint's mono-uppercase
        // annotation look is consistent across every label role — architecture edge labels, group
        // labels, section-band labels, and sequence messages all read the same. MsgText is already a
        // mono role, and mono advance is count-based, so uppercasing is width-invariant: the measured
        // (lowercase) chip still fits the rendered uppercase run with no textLength guard needed.
        StyleTypography typography = c.Typography with
        {
            Roles = new FontRoleTable(role => role switch
            {
                FontRole.EdgeLabel => FontRoles.Of(role) with { Mono = true, Uppercase = true },
                FontRole.GroupLabel => FontRoles.Of(role) with { Mono = true },
                FontRole.MsgText => FontRoles.Of(role) with { Uppercase = true },
                _ => FontRoles.Of(role),
            }),
        };

        return c with
        {
            Name = "blueprint",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Strokes = strokes,
            Typography = typography,
            // Group-box dimension lines (the technical-drawing measured-length annotation). The
            // group-box painter branches on this; nodes/edges/ghosts are untouched, so every shape,
            // variant, packet, marker, and diagram type stays exactly as the CSS/token layer renders it.
            Artwork = StyleArtwork.Blueprint,
        };
    }

    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
