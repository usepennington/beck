namespace Beck;

/// <summary>
/// The <c>terminal</c> built-in style (Phase 3, mock 1f): mono everything — every text role renders
/// through the mono family stack, block/square travelling packets, a hard-step (<c>steps(n)</c>)
/// trail reveal, and a green-ramp default accent (with neutrals biased toward the same success ramp)
/// instead of classic's blue. Squared-off corners (radius 0 throughout) reinforce the "console
/// window" read. Derived from <see cref="BeckStyle.Classic"/> with a <c>with</c> expression so every
/// feature (shapes, groups, icons, packets, trails, sequence choreography, state/class diagrams,
/// scrub, reduced motion, light/dark) stays fully available — only the rendering changes.
/// </summary>
/// <remarks>
/// Deliberately <em>not</em> shipped: scanlines and a blinking cursor (both explicitly excluded by
/// the design brief) and the <c>[bracketed]</c> node-title affordance from the original mock. The
/// bracket treatment would need the title text measured *with* its brackets — <c>CardSizer</c>
/// and every card/pill/class title emission site in <c>SvgRenderer</c> currently measure and render
/// the same <c>node.Title</c> string verbatim, so adding a per-style text decoration correctly (not
/// just at render time, which would desync <c>textLength</c> from the measured box) means touching
/// shared measurement code across several call sites — real card-sizing surgery, not a cheap text
/// tweak. That is Phase-4 artwork territory (see new-designs.md's <c>StyleArtwork</c> discussion);
/// deferred there rather than risking the "measured widths guard the typography" invariant here.
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
            ("--beck-edge", "var(--color-base-300, #cbd5e1)"),
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
            ("--beck-edge", "var(--color-base-700, #30363d)"),
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

        StyleMotion motion = c.Motion with
        {
            // Block/square travelling packets (the "packet emitter" seam: CssCompiler.Markup swaps
            // <circle> for a centred <rect> when a hop resolves to this shape).
            PacketGlyph = PacketGlyph.Square,
            // Hard-step trail reveal — a blocky steps(n) timing function on the trail's
            // stroke-dashoffset track instead of the packet's own (still-smooth) ease.
            TrailSteps = 8,
            // No bloom — crisp edges are part of the "no scanlines, no cursor" restraint.
            GlowEnabled = false,
        };

        return c with
        {
            Name = "terminal",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geo,
            Typography = typography,
            Motion = motion,
        };
    }
}
