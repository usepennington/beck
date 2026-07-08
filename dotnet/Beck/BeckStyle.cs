using Beck.Rendering.Text;

namespace Beck;

/// <summary>
/// The complete visual-styling seam for a Beck diagram: every value that used to be a
/// hardcoded literal in the renderer, grouped into data-only sub-records. Phase 1 introduces
/// this record and its <see cref="Classic"/> instance (today's exact look) with no visual
/// change — the engine always resolves to <see cref="Classic"/>. Later phases add
/// <c>meta.style</c> parsing, an options registry, and the ten built-in styles that derive
/// from a built-in with <c>with</c> expressions.
/// </summary>
/// <remarks>
/// The public surface is data only. Font <em>families</em> and the <see cref="FontRoleTable"/>
/// live on <see cref="Typography"/>; the <c>--beck-*</c> token cascade on
/// <see cref="LightTokens"/>/<see cref="DarkTokens"/>; magic <c>color-mix</c> ratios on
/// <see cref="Mix"/>; dash patterns on <see cref="Strokes"/>; radii/stroke-widths/box-model on
/// <see cref="Geometry"/>; effect durations + dim ratios on <see cref="Motion"/>.
/// </remarks>
public sealed record BeckStyle
{
    /// <summary>The style token used in YAML (<c>[a-z0-9-]</c>); <c>classic</c> is the default.</summary>
    public required string Name { get; init; }

    /// <summary>The light <c>--beck-*</c> token table (name → CSS value, three-tier var chains).</summary>
    public required StyleTokens LightTokens { get; init; }

    /// <summary>The dark <c>--beck-*</c> overrides (a partial set layered over the light block).</summary>
    public required StyleTokens DarkTokens { get; init; }

    /// <summary>Corner radii, stroke widths, border insets, and the card box-model constants.</summary>
    public required StyleGeometry Geometry { get; init; }

    /// <summary>Family stacks and the per-role typography table (weight/size/spacing/case).</summary>
    public required StyleTypography Typography { get; init; }

    /// <summary>The fixed <c>color-mix</c> tint ratios, as named percentages.</summary>
    public required StyleMix Mix { get; init; }

    /// <summary>Dash patterns by role (external, group, lifeline, dashed edge).</summary>
    public required StyleStrokes Strokes { get; init; }

    /// <summary>Effect durations, sequence-dim ratios, and effect stroke widths.</summary>
    public required StyleMotion Motion { get; init; }

    /// <summary>
    /// The reference style: constructed from the engine's exact historical literals. This is the
    /// single source of truth those literals moved to, and the byte-identity anchor for Phase 1.
    /// </summary>
    public static BeckStyle Classic { get; } = BuildClassic();

    private static BeckStyle BuildClassic()
    {
        var mix = new StyleMix
        {
            GroupBorder = 45,
            NodeStroke = 32,
            IconChip = 15,
            StatusPill = 14,
            ClassHead = 10,
            ClassHeadBorder = 28,
            ActivationGlow = 45,
            ChipStroke = 40,
            MsgText = 34,
            BandFill = 5,
            BandStroke = 30,
            BandLabel = 70,
            NarrationFill = 6,
            NarrationBorder = 15,
        };

        // Light token table — verbatim from the original styles.css:16-49. The group-border
        // entry threads mix.GroupBorder so its 45% ratio has a single source (shared with the
        // per-group box border drawn inline in SvgRenderer).
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
            ("--beck-neutral", "var(--color-base-400, #94a3b8)"),
            ("--beck-group-border", $"color-mix(in srgb, var(--beck-neutral) {mix.GroupBorder.ToString(System.Globalization.CultureInfo.InvariantCulture)}%, transparent)"),
            ("--beck-group-label", "var(--beck-text-muted)"),
            ("--beck-edge", "var(--color-base-300, #cbd5e1)"),
            ("--beck-packet", "var(--beck-primary)"),
            ("--beck-icon-bg", "var(--color-base-100, #f1f5f9)"),
            ("--beck-accent", "var(--beck-primary)"),
        });

        // The nine dark overrides — verbatim from styles.css:54-62. A partial override that
        // works because the light block is always emitted first (Stylesheet keeps that order).
        var dark = new StyleTokens(new (string, string)[]
        {
            ("--beck-surface", "var(--color-base-950, #0d1117)"),
            ("--beck-node-bg", "var(--color-base-900, #161b22)"),
            ("--beck-node-border", "var(--color-base-700, #30363d)"),
            ("--beck-node-shadow", "0 1px 3px rgb(0 0 0 / 0.3), 0 4px 14px rgb(0 0 0 / 0.4)"),
            ("--beck-text", "var(--color-base-50, #f0f6fc)"),
            ("--beck-text-muted", "var(--color-base-400, #8b949e)"),
            ("--beck-text-faint", "var(--color-base-500, #6e7681)"),
            ("--beck-edge", "var(--color-base-700, #30363d)"),
            ("--beck-icon-bg", "var(--color-base-800, #21262d)"),
        });

        var geometry = new StyleGeometry
        {
            // corner radii
            CardRadius = 14,
            ClassRadius = 12,
            GhostRadius = 16,
            GroupRadius = 18,
            IconChipRadius = 9,
            GroupLabelBgRadius = 3,
            NarrationRadius = 12,
            BandRadius = 14,

            // stroke widths
            NodeStroke = 1.5,
            EdgeStroke = 1.6,
            GroupStroke = 1.5,
            BandBoxStroke = 1.5,
            LifelineStroke = 2,
            EndNodeStroke = 2,
            HairlineStroke = 1,
            MessageStroke = 2,
            EdgeLabelHalo = "3px",

            // card drop-shadow filters (the card shape uses these; the --beck-node-shadow token
            // is a separate, presently-unreferenced encoding kept verbatim in the token tables).
            NodeShadow = "drop-shadow(0 1px 3px rgb(0 0 0/.05)) drop-shadow(0 4px 12px rgb(0 0 0/.06))",
            NodeShadowDark = "drop-shadow(0 1px 3px rgb(0 0 0/.3)) drop-shadow(0 4px 14px rgb(0 0 0/.4))",

            // border insets are derived from NodeStroke (see StyleGeometry) — no independent literal.

            // card box-model
            CardPadX = 32,
            CardPadY = 28,
            CardMinW = 180,
            CardMaxW = 320,
            IconW = 34,
            IconGap = 12,
            CardTitleLine = 1.3 * 14,
            CardSubLine = 1.35 * 12,
            TextGap = 3,
            StatusMt = 2,
            StatusChipH = 3 * 2 + 1.2 * 10.4,

            // pill box-model (pill title reuses CardTitleLine)
            PillPadX = 40,
            PillPadY = 20,
            PillMinW = 96,
            PillGap = 1,
            PillSubLine = 1.3 * 10.88,

            // ghost box-model
            GhostPadX = 28,
            GhostPadY = 16,
            GhostIcon = 16,
            GhostIconGap = 7,
            GhostGap = 3,
            GhostLabelLine = 1.4 * 11.52,
            StatusInlineLine = 1.4 * 9.92,

            // class box-model
            ClassMinW = 170,
            HeadPadX = 32,
            HeadPadY = 16,
            HeadBorderBottom = 1,
            StereoLine = 1.3 * 10.4,
            ClassTitleLine = 1.4 * 14,
            SectionPadX = 28,
            SectionPadY = 14,
            MemberGap = 2,
            MemberLine = 1.45 * 11.52,

            // start/end pseudo-state box
            StartEndSize = 16,
        };

        var typography = new StyleTypography
        {
            SansFamily = "'Inter', system-ui, -apple-system, sans-serif",
            MonoFamily = "'IBM Plex Mono', ui-monospace, monospace",
            Roles = new FontRoleTable(FontRoles.Of),
            // The packet-label CSS typography (mono/11px/600) — the single source for the
            // `.beck-packet-label` rule the compiler emits. Deliberately distinct from the
            // FontRoles.PacketLabel *measurement* spec (sans/10.56), which the label never uses
            // (packet labels render un-measured, centred on the offset point).
            PacketLabel = new FontRoleSpec(true, 600, 11, 0, false),
        };

        var strokes = new StyleStrokes
        {
            NodeDash = "5 4",
            GroupDash = "6 6",
            LifelineDash = "6 7",
            EdgeDash = "7 5",
            StreamDash = "5 9",
        };

        var motion = new StyleMotion
        {
            PulseDur = 0.6,
            HighlightDur = 0.7,
            FailDur = 1.0,
            DimLine = 0.15,
            DimLabel = 0.35,
            DimAct = 0.25,
            DimBand = 0.45,
            OverlayStroke = 2,
            RingStroke = 2.5,
            PacketRingMin = 2.5,
            PacketRingFactor = 0.28,
        };

        return new BeckStyle
        {
            Name = "classic",
            LightTokens = light,
            DarkTokens = dark,
            Geometry = geometry,
            Typography = typography,
            Mix = mix,
            Strokes = strokes,
            Motion = motion,
        };
    }
}

/// <summary>An ordered <c>--beck-*</c> token table: <c>(name, value)</c> pairs emitted verbatim.</summary>
/// <remarks>
/// Token names and values land <em>inside</em> the SVG <c>&lt;style&gt;</c> block verbatim. For a
/// <em>custom</em> (non-built-in) style — which may be fed from less-trusted config — every such
/// string is scrubbed of substrings that could break out of the style element or inject a rule
/// (<c>&lt;/</c>, <c>&lt;!</c>, <c>{</c>, <c>}</c>, <c>@import</c>); the same rule applies to the
/// typography family strings, dash patterns, and shadow/halo values. Built-in styles are trusted and
/// bypass the scan, so a token value must not rely on those characters.
/// </remarks>
public sealed record StyleTokens(IReadOnlyList<(string Name, string Value)> Entries);

/// <summary>
/// Family stacks + the per-role typography table. <see cref="SansFamily"/>/<see cref="MonoFamily"/>
/// are the default fallback stacks used when the caller does not override fonts; <see cref="Roles"/>
/// feeds both measurement and rendering.
/// </summary>
public sealed record StyleTypography
{
    /// <summary>Default sans family stack (used when no font override is supplied).</summary>
    public required string SansFamily { get; init; }

    /// <summary>Default mono family stack.</summary>
    public required string MonoFamily { get; init; }

    /// <summary>Per-role weight/size/letter-spacing/case table.</summary>
    public required FontRoleTable Roles { get; init; }

    /// <summary>
    /// The packet-label CSS typography (the <c>.beck-packet-label</c> rule). This is the rendered
    /// typography for flow packet labels; it is intentionally separate from the
    /// <see cref="FontRole.PacketLabel"/> entry in <see cref="Roles"/>, which is a measurement spec
    /// the label never consumes (packet labels render un-measured). A style customises packet-label
    /// type here.
    /// </summary>
    public required FontRoleSpec PacketLabel { get; init; }
}

/// <summary>Named, fixed <c>color-mix(in srgb, …)</c> percentages (whole numbers).</summary>
public sealed record StyleMix
{
    /// <summary>Group border ratio (neutral token and per-group accent box alike).</summary>
    public required int GroupBorder { get; init; }
    /// <summary>Node card stroke tint over the accent.</summary>
    public required int NodeStroke { get; init; }
    /// <summary>Icon-chip fill tint.</summary>
    public required int IconChip { get; init; }
    /// <summary>Status pill background tint (single- and multi-state paths).</summary>
    public required int StatusPill { get; init; }
    /// <summary>Class header fill tint.</summary>
    public required int ClassHead { get; init; }
    /// <summary>Class header bottom-border tint.</summary>
    public required int ClassHeadBorder { get; init; }
    /// <summary>Activation-bar glow tint.</summary>
    public required int ActivationGlow { get; init; }
    /// <summary>Message + band chip stroke tint (shared ratio).</summary>
    public required int ChipStroke { get; init; }
    /// <summary>Message text fill tint over the text color.</summary>
    public required int MsgText { get; init; }
    /// <summary>Section band fill tint.</summary>
    public required int BandFill { get; init; }
    /// <summary>Section band box stroke tint.</summary>
    public required int BandStroke { get; init; }
    /// <summary>Section band label fill tint over the text color.</summary>
    public required int BandLabel { get; init; }
    /// <summary>Narration bar fill tint over the surface.</summary>
    public required int NarrationFill { get; init; }
    /// <summary>Narration bar border tint.</summary>
    public required int NarrationBorder { get; init; }
}

/// <summary>Dash patterns by role.</summary>
public sealed record StyleStrokes
{
    /// <summary>External + ghost node outline dash.</summary>
    public required string NodeDash { get; init; }
    /// <summary>Group box + band box dash.</summary>
    public required string GroupDash { get; init; }
    /// <summary>Sequence lifeline dash.</summary>
    public required string LifelineDash { get; init; }
    /// <summary>Author-dashed edge dash (architecture + sequence).</summary>
    public required string EdgeDash { get; init; }
    /// <summary>Animated stream-overlay marching dash (the flow "stream" edge effect).</summary>
    public required string StreamDash { get; init; }
}

/// <summary>Effect durations, sequence-dim ratios, and effect stroke widths.</summary>
public sealed record StyleMotion
{
    /// <summary>Pulse effect sub-timeline length (seconds); shared by schedule + compiler.</summary>
    public required double PulseDur { get; init; }
    /// <summary>Highlight effect sub-timeline length (seconds); shared by schedule + compiler.</summary>
    public required double HighlightDur { get; init; }
    /// <summary>Fail effect sub-timeline length (seconds).</summary>
    public required double FailDur { get; init; }
    /// <summary>Dimmed opacity of message lines during sequence storytelling.</summary>
    public required double DimLine { get; init; }
    /// <summary>Dimmed opacity of message chips/labels.</summary>
    public required double DimLabel { get; init; }
    /// <summary>Dimmed opacity of activation bars.</summary>
    public required double DimAct { get; init; }
    /// <summary>Dimmed opacity of section bands.</summary>
    public required double DimBand { get; init; }
    /// <summary>Stroke width of the pulse/highlight/fail/activate/trail overlays.</summary>
    public required double OverlayStroke { get; init; }
    /// <summary>Stroke width of impact + stream rings.</summary>
    public required double RingStroke { get; init; }
    /// <summary>Minimum stroke width of a ring-shaped packet.</summary>
    public required double PacketRingMin { get; init; }
    /// <summary>Ring packet stroke width as a fraction of packet size.</summary>
    public required double PacketRingFactor { get; init; }
}

/// <summary>Corner radii, stroke widths, border insets, and the card box-model constants.</summary>
public sealed record StyleGeometry
{
    // ---- corner radii ----
    /// <summary>Node card corner radius.</summary>
    public required double CardRadius { get; init; }
    /// <summary>Class card corner radius.</summary>
    public required double ClassRadius { get; init; }
    /// <summary>Ghost node corner radius.</summary>
    public required double GhostRadius { get; init; }
    /// <summary>Group box corner radius.</summary>
    public required double GroupRadius { get; init; }
    /// <summary>Icon-chip corner radius.</summary>
    public required double IconChipRadius { get; init; }
    /// <summary>On-border group-label pill corner radius.</summary>
    public required double GroupLabelBgRadius { get; init; }
    /// <summary>Narration bar corner radius.</summary>
    public required double NarrationRadius { get; init; }
    /// <summary>Sequence section-band box corner radius.</summary>
    public required double BandRadius { get; init; }

    // ---- stroke widths ----
    /// <summary>Node card stroke width (also the source of the render/measure insets).</summary>
    public required double NodeStroke { get; init; }
    /// <summary>Edge stroke width.</summary>
    public required double EdgeStroke { get; init; }
    /// <summary>Group box stroke width.</summary>
    public required double GroupStroke { get; init; }
    /// <summary>Section band box stroke width.</summary>
    public required double BandBoxStroke { get; init; }
    /// <summary>Sequence lifeline stroke width.</summary>
    public required double LifelineStroke { get; init; }
    /// <summary>End pseudo-state ring stroke width.</summary>
    public required double EndNodeStroke { get; init; }
    /// <summary>Hairline stroke (class borders/dividers, msg/band chips).</summary>
    public required double HairlineStroke { get; init; }
    /// <summary>Sequence message path stroke width.</summary>
    public required double MessageStroke { get; init; }
    /// <summary>Edge-label halo stroke width (a CSS length string, e.g. <c>3px</c>).</summary>
    public required string EdgeLabelHalo { get; init; }

    // ---- shadows ----
    /// <summary>Card drop-shadow filter (light).</summary>
    public required string NodeShadow { get; init; }
    /// <summary>Card drop-shadow filter (dark override).</summary>
    public required string NodeShadowDark { get; init; }

    // ---- insets (derived from NodeStroke — the single source of truth) ----
    /// <summary>Render inset per side: half the node stroke, so a centre-aligned stroke stays inside
    /// the rect (rects shrink by twice this). Derived from <see cref="NodeStroke"/>, so a style that
    /// thickens the stroke moves the inset with it and never clips its own border.</summary>
    public double NodeBorderInset => NodeStroke / 2;

    /// <summary>Measurement border budget (both sides) the card sizer reserves. The browser resolves a
    /// centre-aligned CSS stroke to a whole-pixel used-width per side at DPR 1, so this re-derives from
    /// <see cref="NodeStroke"/> as <c>2·round(stroke/2)</c> — 2 for the classic 1.5 stroke — keeping the
    /// measured box matched to the rendered box when a style changes the stroke.</summary>
    public double MeasureBorder => 2 * Rendering.Js.Round(NodeStroke / 2);

    // ---- card box-model ----
    /// <summary>Card total horizontal padding (both sides) reserved around the content column.</summary>
    public required double CardPadX { get; init; }
    /// <summary>Card total vertical padding (both sides) added to the content height.</summary>
    public required double CardPadY { get; init; }
    /// <summary>Minimum card border-box width.</summary>
    public required double CardMinW { get; init; }
    /// <summary>Maximum auto-grown card width; past it the title/subtitle wrap.</summary>
    public required double CardMaxW { get; init; }
    /// <summary>Icon-chip side length (square).</summary>
    public required double IconW { get; init; }
    /// <summary>Gap between the icon chip and the text column.</summary>
    public required double IconGap { get; init; }
    /// <summary>Card-title line height (px).</summary>
    public required double CardTitleLine { get; init; }
    /// <summary>Card-subtitle line height (px).</summary>
    public required double CardSubLine { get; init; }
    /// <summary>Vertical gap between stacked text rows (title/subtitle/status).</summary>
    public required double TextGap { get; init; }
    /// <summary>Extra top margin above the status chip.</summary>
    public required double StatusMt { get; init; }
    /// <summary>Status-chip height (its own vertical padding plus the status line).</summary>
    public required double StatusChipH { get; init; }

    // ---- pill box-model ----
    /// <summary>Pill total horizontal padding (both sides).</summary>
    public required double PillPadX { get; init; }
    /// <summary>Pill total vertical padding (both sides).</summary>
    public required double PillPadY { get; init; }
    /// <summary>Minimum pill border-box width.</summary>
    public required double PillMinW { get; init; }
    /// <summary>Vertical gap between the pill title and its subtitle/status.</summary>
    public required double PillGap { get; init; }
    /// <summary>Pill-subtitle line height (px).</summary>
    public required double PillSubLine { get; init; }

    // ---- ghost box-model ----
    /// <summary>Ghost node total horizontal padding (both sides).</summary>
    public required double GhostPadX { get; init; }
    /// <summary>Ghost node total vertical padding (both sides).</summary>
    public required double GhostPadY { get; init; }
    /// <summary>Ghost node icon side length (square).</summary>
    public required double GhostIcon { get; init; }
    /// <summary>Gap between the ghost icon and its label.</summary>
    public required double GhostIconGap { get; init; }
    /// <summary>Vertical gap between the ghost label row and its inline status.</summary>
    public required double GhostGap { get; init; }
    /// <summary>Ghost-label line height (px).</summary>
    public required double GhostLabelLine { get; init; }
    /// <summary>Inline-status line height on a ghost node (px).</summary>
    public required double StatusInlineLine { get; init; }

    // ---- class box-model ----
    /// <summary>Minimum class-card border-box width.</summary>
    public required double ClassMinW { get; init; }
    /// <summary>Class header total horizontal padding (both sides).</summary>
    public required double HeadPadX { get; init; }
    /// <summary>Class header total vertical padding (both sides).</summary>
    public required double HeadPadY { get; init; }
    /// <summary>Class header bottom-border thickness (also the between-section divider thickness).</summary>
    public required double HeadBorderBottom { get; init; }
    /// <summary>Class stereotype line height (px).</summary>
    public required double StereoLine { get; init; }
    /// <summary>Class-title line height (px).</summary>
    public required double ClassTitleLine { get; init; }
    /// <summary>Member-section total horizontal padding (both sides).</summary>
    public required double SectionPadX { get; init; }
    /// <summary>Member-section total vertical padding (both sides).</summary>
    public required double SectionPadY { get; init; }
    /// <summary>Vertical gap between consecutive class members.</summary>
    public required double MemberGap { get; init; }
    /// <summary>Class-member line height (px).</summary>
    public required double MemberLine { get; init; }

    // ---- start/end pseudo-state ----
    /// <summary>Side length of the start/end pseudo-state marker box.</summary>
    public required double StartEndSize { get; init; }
}
