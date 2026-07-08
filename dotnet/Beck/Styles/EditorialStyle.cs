using Beck.Rendering.Text;

namespace Beck;

/// <summary>
/// The <c>editorial</c> built-in style (Phase 3, mock 1j): a serif textbook-figure look. Body type
/// is Source Serif 4 (sized against the embedded <see cref="MetricsFont.SourceSerif"/> table so
/// layout stays correct with zero font dependencies), strokes drop to hairlines, fills are pulled
/// almost to nothing (subtle chips/pills, no drop-shadows, flat activation bars), the narration bar
/// restyles as a numbered <c>Fig. N —</c> serif-italic figure caption, and the sequence-storytelling
/// scenery draws on slowly and softly via a stretched reveal ramp. Derived from
/// <see cref="BeckStyle.Classic"/> with a <c>with</c> expression so every feature (all node
/// shapes/variants, groups, icons, edges/labels/UML markers, packets + labels, trails,
/// highlight/pulse/fail, status pills, narration, impact/working rings, sequence choreography incl.
/// dimming, state + class diagrams, scrub, reduced motion, light/dark) stays available by
/// construction — only the typography, tokens, hairline geometry, flattened fills, narration
/// caption, and reveal timing change.
/// </summary>
/// <remarks>
/// <para>The serif identity is a single token/measurement move: <see cref="StyleTypography.SansFamily"/>
/// becomes a Source Serif 4 stack (referenced only through <c>--beck-font</c>, so a host webfont still
/// loads and the <c>textLength</c> guard absorbs any residual mismatch) and
/// <see cref="StyleTypography.MetricsFont"/> selects the matching embedded table so the card sizer
/// measures against serif metrics. Mono roles (class members, message/band text, packet labels) keep
/// IBM Plex Mono.</para>
/// <para>The <c>Fig. N —</c> caption numbering and its serif-italic set are driven by the data-only
/// <see cref="StyleTypography.NarrationFigureCaption"/> flag (the prefix is prepended before the
/// narration wrap/measure, so measured == rendered); the slow draw-on is the existing sequence-reveal
/// choreography with its ramp lengthened via <see cref="StyleMotion.SequenceRevealScale"/> — no new
/// animation mechanism. Both fields default to classic's exact behaviour, so classic stays
/// byte-identical.</para>
/// </remarks>
public static class EditorialStyle
{
    public static readonly BeckStyle Instance = Build();

    private static BeckStyle Build()
    {
        BeckStyle c = BeckStyle.Classic;

        // Serif body type + the matching embedded metrics table. Referenced through --beck-font only;
        // the numbered serif-italic figure caption rides the data-only NarrationFigureCaption flag.
        StyleTypography typography = c.Typography with
        {
            SansFamily = "'Source Serif 4', 'Source Serif Pro', Georgia, 'Times New Roman', serif",
            MetricsFont = MetricsFont.SourceSerif,
            NarrationFigureCaption = true,
        };

        // Ink-on-paper editorial palette: a slightly darker "ink" edge and border so hairlines read as
        // fine drawn rules rather than classic's soft gray, muted body ink, and a shadow token pinned to
        // none (the flat, print-like surface). Every entry keeps the three-tier
        // var(--beck-X, var(--color-Y, literal)) indirection, so a host --color-* / --beck-* wins.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #ffffff)"),
            // Paper/outline node surfaces: the card fill IS the page paper (var(--beck-surface)), so a
            // card reads as a hairline-ruled region on the page rather than a filled panel — the
            // textbook line-art identity, in BOTH themes. In light this already matched the white
            // surface; in dark it drops the base-900 panel fill that read as a heavy dark block, so the
            // hairline ink border alone defines the card. Ties to the surface token, not a literal.
            ("--beck-node-bg", "var(--beck-surface)"),
            ("--beck-node-border", "var(--color-base-400, #94a3b8)"),
            ("--beck-node-shadow", "none"),
            ("--beck-text", "var(--color-base-900, #0f172a)"),
            ("--beck-text-muted", "var(--color-base-500, #64748b)"),
            ("--beck-text-faint", "var(--color-base-400, #94a3b8)"),
            ("--beck-primary", "var(--color-primary-700, #1d4ed8)"),
            ("--beck-success", "var(--color-emerald-600, #059669)"),
            ("--beck-warn", "var(--color-amber-600, #d97706)"),
            ("--beck-danger", "var(--color-red-600, #dc2626)"),
            ("--beck-info", "var(--color-violet-600, #7c3aed)"),
            ("--beck-neutral", "var(--color-base-500, #64748b)"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {P(c.Mix.GroupBorder)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text-muted)"),
            ("--beck-edge", "var(--color-base-500, #64748b)"),
            ("--beck-packet", "var(--beck-primary)"),
            ("--beck-icon-bg", "var(--color-base-100, #f1f5f9)"),
            ("--beck-accent", "var(--beck-primary)"),
        });

        // Dark overrides only (layered over the light block, which is always emitted first): a paper-dark
        // surface with lighter ink rules for the hairlines, and the shadow pinned to none to match.
        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #0d1117)"),
            // Paper/outline in dark too: card fill = the page paper, hairline ink border only (see light).
            ("--beck-node-bg", "var(--beck-surface)"),
            ("--beck-node-border", "var(--color-base-500, #6e7681)"),
            ("--beck-node-shadow", "none"),
            ("--beck-text", "var(--color-base-50, #f0f6fc)"),
            ("--beck-text-muted", "var(--color-base-400, #8b949e)"),
            ("--beck-text-faint", "var(--color-base-500, #6e7681)"),
            ("--beck-edge", "var(--color-base-500, #6e7681)"),
            ("--beck-icon-bg", "var(--color-base-800, #21262d)"),
        });

        StyleGeometry geo = c.Geometry with
        {
            // Restrained, near-square corners — a drawn figure, not a rounded UI card.
            CardRadius = 3,
            ClassRadius = 3,
            GhostRadius = 3,
            GroupRadius = 4,
            IconChipRadius = 2,
            GroupLabelBgRadius = 2,
            NarrationRadius = 2,
            BandRadius = 3,

            // Hairline rules throughout — the headline editorial trait.
            NodeStroke = 1,
            EdgeStroke = 1,
            GroupStroke = 1,
            BandBoxStroke = 1,
            LifelineStroke = 1,
            EndNodeStroke = 1,
            HairlineStroke = 1,
            MessageStroke = 1,

            // Flat, print-like surface — no drop-shadows anywhere.
            NodeShadow = "none",
            NodeShadowDark = "none",
            NarrationShadow = "none",
        };

        StyleMix mix = c.Mix with
        {
            // Pull fills almost to nothing — line-art, not filled chrome. Chips/pills stay faintly
            // tinted so the features remain legible; the activation glow drops to flat.
            IconChip = 6,
            StatusPill = 8,
            ClassHead = 5,
            ActivationGlow = 0,
            // The figure caption reads as a thin ruled line, not a filled bar.
            NarrationFill = 0,
            NarrationBorder = 22,
        };

        StyleMotion motion = c.Motion with
        {
            // Thin overlays/rings to match the hairline geometry, no packet bloom (flat line-art), and
            // an understated (but present) effect amplitude — restrained, textbook motion.
            OverlayStroke = 1,
            RingStroke = 1,
            PacketRingMin = 1,
            PacketRingFactor = 0.2,
            GlowEnabled = false,
            EffectAmplitude = 0.7,
            // The slow, soft draw-on: the existing sequence-reveal ramp, stretched.
            SequenceRevealScale = 2.6,
        };

        return c with
        {
            Name = "editorial",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Typography = typography,
            Mix = mix,
            Motion = motion,
        };
    }

    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
