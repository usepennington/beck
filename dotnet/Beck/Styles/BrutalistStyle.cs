using Beck.Rendering.Text;

namespace Beck;

/// <summary>
/// The <c>brutalist</c> built-in style (Phase 4): stark, high-contrast neo-brutalism — heavy black
/// borders, a solid blur-free <see cref="StyleArtwork.Brutalist"/> offset shadow behind each node,
/// squared corners, <c>steps()</c> stepped packet/trail flow motion, and loud, <em>uppercase
/// weight-800 Archivo</em> headings. Its identity is carried by role-remapped typography that only works because
/// this phase makes the style's <see cref="FontRoleTable"/> feed <em>measurement</em>, not just
/// rendering: the card/pill/class title roles are remapped to weight 800 + uppercase and sized against
/// the embedded Archivo table (<see cref="MetricsFont.Archivo"/>), so their boxes measure to the real
/// heavy uppercase run instead of a classic-weight lowercase one the <c>textLength</c> guard would then
/// squeeze. Derived from <see cref="BeckStyle.Classic"/> with a <c>with</c> expression, so every
/// feature (shapes, groups, icons, packets, trails, sequence choreography, state/class diagrams, scrub,
/// reduced motion, light/dark) stays fully available by construction — only tokens, geometry, and
/// typography change.
/// </summary>
/// <remarks>
/// <para><b>Measurement seam (the point of this style):</b> card titles render via
/// <c>SvgRenderer.Line</c>/<c>CenterLine</c>, which emit <c>font-weight</c> from the resolved
/// <see cref="FontRoleSpec"/> and uppercase the drawn string when the spec is uppercase — matching what
/// <see cref="CardSizer"/> now measures through the same role table. The two agree, so the box holds the
/// heavy uppercase run with no glyph squeezing and correct padding.</para>
/// <para><b>Artwork (StyleArtwork.Brutalist):</b> the offset shadow is a real solid rect emitted behind
/// each card/pill/class node by the <c>Artwork.Rect</c> shape seam (gated data selector, not injected
/// markup) — token-coloured (<c>--beck-shadow</c>), blur-free, and inside the card's own
/// <c>.beck-fx-node</c> wrapper so effects move card and shadow together. The router is untouched;
/// group boxes, ghosts, and pseudo-states stay unshadowed. Combined with the <c>steps(6)</c> packet +
/// trail motion, the brutalist read is carried without any per-shape jitter or continuous animation.</para>
/// </remarks>
public static class BrutalistStyle
{
    public static readonly BeckStyle Instance = Build();

    private static BeckStyle Build()
    {
        BeckStyle c = BeckStyle.Classic;

        // High-contrast tokens: near-black hard borders/edges over the host ramp (inverted in dark so
        // borders read near-white on black). Every entry keeps the three-tier
        // var(--beck-X, var(--color-Y, literal)) indirection, so a host --color-* / --beck-* override
        // still wins; the literals are only the last resort. Accent/semantics stay classic.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #ffffff)"),
            ("--beck-node-bg", "var(--color-base-50, #ffffff)"),
            ("--beck-node-border", "var(--color-base-900, #0f172a)"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.05), 0 4px 12px rgb(0 0 0 / 0.06)"),
            ("--beck-text", "var(--color-base-900, #0f172a)"),
            ("--beck-text-muted", "var(--color-base-600, #475569)"),
            ("--beck-text-faint", "var(--color-base-500, #64748b)"),
            ("--beck-primary", "var(--color-primary-600, #175ddc)"),
            ("--beck-success", "var(--color-emerald-500, #10b981)"),
            ("--beck-warn", "var(--color-amber-500, #f59e0b)"),
            ("--beck-danger", "var(--color-red-500, #ef4444)"),
            ("--beck-info", "var(--color-violet-500, #8b5cf6)"),
            ("--beck-neutral", "var(--color-base-900, #0f172a)"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {P(c.Mix.GroupBorder)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text)"),
            ("--beck-edge", "var(--color-base-900, #0f172a)"),
            ("--beck-packet", "var(--beck-primary)"),
            ("--beck-icon-bg", "var(--color-base-100, #f1f5f9)"),
            ("--beck-accent", "var(--beck-primary)"),
            // The hard-offset shadow ink for the StyleArtwork.Brutalist shadow rect (behind each node).
            // A solid, *neutral* near-black — one rung darker than the border's base-900 slate and free
            // of its blue tint — so the thin offset sliver reads as crisp black sticker ink rather than a
            // faint grey-navy shadow on a near-white surface (visual-jury tuning). Still token-driven:
            // a host --color-base-950 (or --beck-shadow) override wins; #0a0a0a is only the last resort.
            ("--beck-shadow", "var(--color-base-950, #0a0a0a)"),
        });

        // Dark overrides only (layered over the light block, which is always emitted first): borders and
        // edges flip to a near-white ramp for the same stark contrast on a black surface.
        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #0d1117)"),
            ("--beck-node-bg", "var(--color-base-900, #161b22)"),
            ("--beck-node-border", "var(--color-base-100, #f1f5f9)"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.3), 0 4px 14px rgb(0 0 0 / 0.4)"),
            ("--beck-text", "var(--color-base-50, #f0f6fc)"),
            ("--beck-text-muted", "var(--color-base-300, #cbd5e1)"),
            ("--beck-text-faint", "var(--color-base-400, #8b949e)"),
            ("--beck-neutral", "var(--color-base-100, #f1f5f9)"),
            ("--beck-edge", "var(--color-base-100, #f1f5f9)"),
            ("--beck-icon-bg", "var(--color-base-800, #21262d)"),
            // Inverted on black: the sticker shadow flips to the near-white border ink.
            ("--beck-shadow", "var(--color-base-100, #f1f5f9)"),
        });

        // Squared corners + thick borders (the neo-brutalist chassis). NodeStroke drives both the render
        // insets and the measured border budget (StyleGeometry re-derives both), so a thicker stroke
        // stays self-consistent between the measured and drawn box. The signature "sticker" lift is a
        // solid blur-free StyleArtwork.Brutalist offset rect (ShadowOffset below), token-coloured via
        // --beck-shadow — not a CSS drop-shadow filter — so no resolved colour touches shape CSS.
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

            NodeStroke = 3,
            GroupStroke = 3,
            EdgeStroke = 2.2,
            BandBoxStroke = 3,
            LifelineStroke = 3,
            EndNodeStroke = 3,
            HairlineStroke = 2,
            MessageStroke = 3,

            // The node "shadow" is no longer a CSS drop-shadow filter — it is the solid, token-coloured
            // StyleArtwork.Brutalist offset rect drawn behind each card (below), so the .beck-node
            // filter is cleared. The narration bar (not a node, not artwork) keeps a hard-offset filter
            // for the same brutalist read.
            NodeShadow = "none",
            NodeShadowDark = "none",
            NarrationShadow = "drop-shadow(4px 4px 0 rgb(0 0 0/.9))",
            ShadowOffset = 5,
        };

        // The measurement-driven identity: card / pill / class titles are heavy (800) and uppercased,
        // sized against Archivo. CardSizer resolves these specs through this table, so the boxes grow to
        // the real heavy uppercase run; SvgRenderer emits font-weight=800 and the uppercased string, so
        // the drawn text matches the measured box (no textLength squeeze). Subtitles/status/members stay
        // classic (still Archivo-measured via MetricsFont below), so the hierarchy reads.
        StyleTypography typography = c.Typography with
        {
            SansFamily = "'Archivo', system-ui, -apple-system, sans-serif",
            MetricsFont = MetricsFont.Archivo,
            Roles = new FontRoleTable(role => role switch
            {
                FontRole.CardTitle => FontRoles.Of(role) with { Weight = 800, Uppercase = true },
                FontRole.PillTitle => FontRoles.Of(role) with { Weight = 800, Uppercase = true },
                FontRole.ClassTitle => FontRoles.Of(role) with { Weight = 800, Uppercase = true },
                _ => FontRoles.Of(role),
            }),
        };

        // Stepped flow motion (brutalist's mechanical read): the travelling packet glyph hops its edge
        // in discrete steps rather than gliding, and its trail reveals in the same hard cuts. Only the
        // flow effect steps — nothing animates at rest. Card pulse/highlight keep their shared timing
        // (the "pop" is a flow-triggered arrival cue, not resting jitter).
        StyleMotion motion = c.Motion with
        {
            PacketSteps = 6,
            TrailSteps = 6,
        };

        return c with
        {
            Name = "brutalist",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Typography = typography,
            Motion = motion,
            // The offset-shadow artwork seam: card/pill/class nodes gain a solid blur-free shadow rect
            // (ShadowOffset px, --beck-shadow) behind them. Data-only selector — no injected markup.
            Artwork = StyleArtwork.Brutalist,
        };
    }

    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
