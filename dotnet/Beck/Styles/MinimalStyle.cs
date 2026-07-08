namespace Beck;

/// <summary>
/// The <c>minimal</c> built-in style (Phase 3, mock 1c): a sober flat "no design" look — hairline
/// strokes, no card/narration drop-shadows, a restrained low-chroma token table, and motion that
/// stays present but understated (no packet bloom, no activation glow, impact + working rings gated
/// off entirely, quieter ripple peaks). Derived from <see cref="BeckStyle.Classic"/> with a <c>with</c> expression so every
/// feature (shapes, groups, icons, packets, trails, sequence choreography, state/class diagrams,
/// scrub, reduced motion, light/dark) stays fully available — only the rendering is toned down.
/// </summary>
public static class MinimalStyle
{
    public static readonly BeckStyle Instance = Build();

    private static BeckStyle Build()
    {
        BeckStyle c = BeckStyle.Classic;

        // A slightly more visible neutral border than classic's, since minimal drops the card
        // drop-shadow that classic uses to imply depth — the hairline border alone must read as
        // the card edge. Everything else keeps classic's token *names* and host-ramp indirection,
        // only the fallback literal/ramp step shifts.
        //
        // Restrained neutral accent table: the accent LITERALS are remapped to low-chroma,
        // slate-leaning tones so a standalone minimal diagram (no host palette) reads muted and
        // sober rather than as lightly-detuned classic. Only the last-resort literal changes — every
        // token keeps its var(--color-X, literal) chain, so a host that defines --color-* / --beck-*
        // still wins at full saturation. The indirection stays sacred; only the default detunes.
        var light = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-50, #ffffff)"),
            ("--beck-node-bg", "var(--color-base-50, #ffffff)"),
            ("--beck-node-border", "var(--color-base-300, #cbd5e1)"),
            ("--beck-node-shadow", "none"),
            ("--beck-text", "var(--color-base-800, #1e293b)"),
            ("--beck-text-muted", "var(--color-base-500, #64748b)"),
            ("--beck-text-faint", "var(--color-base-400, #94a3b8)"),
            ("--beck-primary", "var(--color-primary-600, #5b6b88)"),
            ("--beck-success", "var(--color-emerald-500, #5f9080)"),
            ("--beck-warn", "var(--color-amber-500, #bd9350)"),
            ("--beck-danger", "var(--color-red-500, #c06b6b)"),
            ("--beck-info", "var(--color-violet-500, #8279aa)"),
            ("--beck-neutral", "var(--color-base-400, #94a3b8)"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {c.Mix.GroupBorder.ToString(System.Globalization.CultureInfo.InvariantCulture)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text-muted)"),
            ("--beck-edge", "var(--color-base-300, #cbd5e1)"),
            ("--beck-packet", "var(--beck-primary)"),
            ("--beck-icon-bg", "var(--color-base-100, #f1f5f9)"),
            ("--beck-accent", "var(--beck-primary)"),
        });

        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #0d1117)"),
            ("--beck-node-bg", "var(--color-base-900, #161b22)"),
            ("--beck-node-border", "var(--color-base-600, #484f58)"),
            ("--beck-node-shadow", "none"),
            ("--beck-text", "var(--color-base-50, #f0f6fc)"),
            ("--beck-text-muted", "var(--color-base-400, #8b949e)"),
            ("--beck-text-faint", "var(--color-base-500, #6e7681)"),
            ("--beck-edge", "var(--color-base-700, #30363d)"),
            ("--beck-icon-bg", "var(--color-base-800, #21262d)"),
        });

        StyleGeometry geo = c.Geometry with
        {
            // flatter, tighter corner radii — "no design" reads as squarer, not rounded-card chic.
            CardRadius = 6,
            ClassRadius = 6,
            GhostRadius = 6,
            GroupRadius = 8,
            IconChipRadius = 4,
            GroupLabelBgRadius = 2,
            NarrationRadius = 6,
            BandRadius = 6,

            // hairline strokes throughout — the identity's headline trait.
            NodeStroke = 1,
            EdgeStroke = 1,
            GroupStroke = 1,
            BandBoxStroke = 1,
            LifelineStroke = 1,
            EndNodeStroke = 1,
            HairlineStroke = 1,
            MessageStroke = 1,

            // no shadows anywhere.
            NodeShadow = "none",
            NodeShadowDark = "none",
            NarrationShadow = "none",
        };

        StyleMix mix = c.Mix with
        {
            // activation bars lose their glow (color-mix against 0% accent = fully transparent).
            ActivationGlow = 0,
        };

        StyleMotion motion = c.Motion with
        {
            OverlayStroke = 1,
            RingStroke = 1,
            PacketRingMin = 1,
            PacketRingFactor = 0.2,
            GlowEnabled = false,
            EffectAmplitude = 0.4,
            // Spec: impact/working rings OFF. EffectAmplitude only *scales* the rings' peaks (0.4 is a
            // faint hollow ring, not "off"); this hard-gates them so no impact or working ring renders
            // at all — the packets, trails, pulses, and status pills still animate, understated.
            RingsEnabled = false,
            // Raise the sequence-storytelling dim floor. On the dark token set the dimmed message
            // edges (var(--beck-edge) = base-700) and the activation bar's gradient tail (0.35 stop)
            // drop to near-invisible at classic's 0.15/0.25 floors — a static viewer can't read the
            // resting flow. Lift the line + activation floors so dimmed scenery stays faintly legible
            // while the reveal still swells each active row to full opacity (clear storytelling
            // contrast remains). Uniform across themes — light only gains a touch more presence.
            DimLine = 0.3,
            DimAct = 0.4,
        };

        return c with
        {
            Name = "minimal",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Mix = mix,
            Motion = motion,
        };
    }
}
