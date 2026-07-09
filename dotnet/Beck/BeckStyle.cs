using Beck.Rendering.Text;

namespace Beck;

/// <summary>
/// The packet glyph a style prefers by default (<see cref="StyleMotion.PacketGlyph"/>) — a small,
/// public, style-scoped vocabulary distinct from the per-packet author-facing
/// <see cref="Beck.PacketShape"/> (<c>Dot|Circle|Ring</c>), since a style-level default may pick a
/// shape no individual flow step can yet author. <c>Train</c> (metro's elongated capsule packet) is
/// Phase-4 artwork and intentionally not a member here yet.
/// </summary>
public enum PacketGlyph
{
    /// <summary>The classic filled circle.</summary>
    Dot,
    /// <summary>A stroked (unfilled) ring.</summary>
    Ring,
    /// <summary>A centred square/block — terminal's identity glyph.</summary>
    Square,
    /// <summary>An elongated rounded-rect capsule oriented along the path — metro's train packet.</summary>
    Train,
}

/// <summary>
/// The node-chrome shape family a style draws — the engine-internal-but-data selector
/// (<see cref="BeckStyle.Artwork"/>) new-designs.md reserves for shape variations that can't be
/// expressed through CSS/tokens alone. It is a small closed vocabulary, <em>not</em> a public
/// interface: a style picks one of these values and the shape emitters branch on it, so custom
/// user styles compose an existing artwork (keeping determinism + the no-injected-markup
/// guarantee) rather than supplying arbitrary geometry. <see cref="Plain"/> is classic and every
/// CSS/token-only style — byte-identical.
/// </summary>
public enum StyleArtwork
{
    /// <summary>Straight rounded rects and true circles — classic and every non-artwork style.</summary>
    Plain,

    /// <summary>
    /// Neo-brutalist: every card/pill/class node keeps its straight rect (so all token-driven
    /// fill/stroke/filter still apply), but gains a <em>solid, blur-free, token-coloured offset
    /// shadow rect</em> drawn <em>behind</em> it — the hard "sticker" lift. The shadow is a plain
    /// <c>&lt;rect class="beck-shadow"&gt;</c> offset down-right by
    /// <see cref="StyleGeometry.ShadowOffset"/> px, filled through <c>var(--beck-shadow, …)</c> so it
    /// theme-adapts and never emits a resolved literal; it rides inside the same
    /// <c>.beck-fx-node</c> wrapper as the card, so pulses/highlights move card and shadow together.
    /// Group boxes, ghost nodes, and start/end pseudo-states are deliberately <em>not</em> shadowed
    /// (a solid slab behind a dashed/transparent shape reads as noise). Static — nothing animates at
    /// rest; the offset is baked geometry, not motion.
    /// </summary>
    Brutalist,

    /// <summary>
    /// Hand-drawn (sketch): node rects/pills/class cards, group boxes, and start/end pseudo-state
    /// circles become subtly-wobbly closed <c>&lt;path&gt;</c>s whose jitter is baked into the path
    /// geometry — deterministic from the content hash + node id, so the same input wobbles the same
    /// way forever and nothing animates continuously. The paths keep the same <c>class</c> as the
    /// rects they replace, so every token-driven fill/stroke/filter still applies; edges stay exact
    /// straight router paths (edge wobble is deferred to protect the one-path/offset-path contract).
    /// </summary>
    Sketch,

    /// <summary>
    /// 2.5D slabs (extrude): every card/pill/class node keeps its straight rounded rect (so all
    /// token-driven fill/stroke/filter still applies), but gains two <em>solid depth faces</em> —
    /// a right and a bottom parallelogram — drawn <em>behind</em> it, offset down-right by
    /// <see cref="StyleGeometry.DepthOffset"/> px as if the light source were top-left. The faces are
    /// filled through <c>var(--beck-depth-right, …)</c> / <c>var(--beck-depth-bottom, …)</c> tokens
    /// (a darker <c>color-mix</c> of the node surface), so they theme-adapt and never emit a resolved
    /// literal; they ride inside the same <c>.beck-fx-node</c> wrapper as the card, so a press-down
    /// (<see cref="StyleMotion.PressDown"/>) moves card and faces together toward the base. Depth is
    /// <em>static</em> — nothing bobs at rest; the offset is baked geometry, not motion. Group boxes,
    /// ghost nodes, and start/end pseudo-states are deliberately <em>not</em> extruded (a solid slab
    /// behind a dashed/transparent/hollow shape reads as noise). A <c>0</c> offset (classic, and every
    /// non-extrude style) emits no faces — byte-identical.
    /// </summary>
    Extruded,

    /// <summary>
    /// PCB / circuit board (circuit): every card/pill/class node keeps its straight rounded rect (so
    /// all token-driven fill/stroke/filter still applies) but gains short decorative <em>pin stubs</em>
    /// — small rects protruding from its left and right edges like a DIP chip package — drawn
    /// <em>behind</em> it and filled through <c>var(--beck-pin, …)</c> so only the outer sliver shows
    /// past the node's opaque fill. The pin count/spacing derive deterministically from the node's own
    /// height (no RNG, no measurement jitter); group boxes / ghosts / start-end pseudo-states are
    /// deliberately left bare (pins on a hollow/dashed shape read as noise). In addition the
    /// <em>edge</em> emitter drops a small <em>via dot</em> (<c>&lt;circle class="beck-via"&gt;</c>,
    /// filled through <c>var(--beck-via, …)</c>) at every genuine bend of an edge's already-computed
    /// route polyline — the elbow where a right-angle trace turns — read from the existing route
    /// geometry in the SVG layer with the router untouched and the edge still one continuous
    /// <c>&lt;path&gt;</c>. Straight/curved edges (no interior bend) and short nodes simply show fewer
    /// decorations. Every other style (classic included) emits neither pins nor vias — byte-identical.
    /// </summary>
    Circuit,

    /// <summary>
    /// Transit map (metro): the diagram reads as a subway map. Nodes keep their straight rounded rect
    /// (so all token-driven fill/stroke/filter still applies) and edges stay one continuous
    /// <c>&lt;path&gt;</c>, but the style pairs thick round-capped edge strokes
    /// (<see cref="StyleGeometry.EdgeStroke"/>) with a small <em>station dot</em> dropped at each
    /// edge's two <em>anchor endpoints</em> — a filled circle
    /// (<c>&lt;circle class="beck-station"&gt;</c>, radius <see cref="StyleGeometry.StationRadius"/>)
    /// with a contrasting ring, drawn <em>over</em> the line: the fill is the token surface
    /// (<c>var(--beck-station-fill, var(--beck-surface))</c>, the "white station") and the ring takes
    /// the edge's own colour so each transit line's stations match its hue. The dots are read straight
    /// off the already-computed route geometry (the polyline's first/last point in the architecture
    /// router, the message endpoints in the sequence painter) — the router is untouched and the dots
    /// are additional sibling elements, never a split edge path. Deterministic (geometry only, no RNG);
    /// a <c>0</c> <see cref="StyleGeometry.StationRadius"/> (classic, and every non-metro style) emits
    /// no stations — byte-identical. Metro pairs this with the <see cref="Beck.PacketGlyph.Train"/>
    /// packet glyph, but the two are independent seams.
    /// </summary>
    Metro,

    /// <summary>
    /// Technical drawing (blueprint): every node keeps its straight rounded rect (so all token-driven
    /// fill/stroke/filter still applies) and edges stay one continuous <c>&lt;path&gt;</c>, but each
    /// <em>group box</em> gains a subtle <em>dimension line</em> along its top edge — a thin extension
    /// rule offset above the edge, joined to the box's two top corners by short perpendicular witness
    /// ticks (the classic drafted-drawing measured-length annotation). Emitted only in the group-box
    /// painter, so nodes/edges/ghosts are untouched; drawn only when
    /// <see cref="StyleGeometry.DimensionTick"/> is non-zero. Token-coloured through
    /// <c>var(--beck-dimension, …)</c> so it theme-adapts and never emits a resolved literal, and read
    /// purely from the already-computed group rect (no router involvement). Every other style (classic
    /// included) emits no dimension lines — byte-identical.
    /// </summary>
    Blueprint,
}

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
    /// The node-chrome shape family (<see cref="StyleArtwork.Plain"/> rects/circles vs.
    /// <see cref="StyleArtwork.Sketch"/> wobbly hand-drawn paths). Defaults to
    /// <see cref="StyleArtwork.Plain"/>, so classic and every CSS/token-only style stay byte-identical
    /// without setting it. Consumed by internal branches in the shape emitters, never as public markup.
    /// </summary>
    public StyleArtwork Artwork { get; init; } = StyleArtwork.Plain;

    /// <summary>
    /// The per-style <em>edge-presentation</em> seam: how every architecture edge / sequence message
    /// is painted — base-layer treatment, an optional overlay layer sharing the edge's exact <c>d</c>,
    /// arrowhead presentation + marker scaling, deterministic path <em>bow</em> shaping, and the
    /// lifeline/separator treatment. Defaults to <see cref="StyleEdges.Classic"/> (all knobs at their
    /// historical values), so classic and every style that doesn't customise edges stay byte-identical.
    /// Consumed by the edge/message emitters, <see cref="Rendering.Svg.Markers"/>, and the overlay
    /// compiler in <see cref="Rendering.Animate.CssCompiler"/>.
    /// </summary>
    public StyleEdges Edges { get; init; } = StyleEdges.Classic;

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
            NarrationShadow = "drop-shadow(0 4px 12px rgb(0 0 0/.06))",

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
            GlowEnabled = true,
            EffectAmplitude = 1.0,
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
    /// The embedded metrics table the default measurer sizes against — picked to match
    /// <see cref="SansFamily"/> so layout stays correct with no font dependency. Defaults to
    /// <see cref="MetricsFont.Inter"/> (classic). Only steers the built-in fallback measurer: an
    /// explicit <see cref="Rendering.SvgRenderOptions.Measurer"/> (e.g. Skia) overrides it. Mono roles resolve
    /// against the shared IBM Plex Mono coverage regardless of this key.
    /// </summary>
    public MetricsFont MetricsFont { get; init; } = MetricsFont.Inter;

    /// <summary>
    /// The packet-label CSS typography (the <c>.beck-packet-label</c> rule). This is the rendered
    /// typography for flow packet labels; it is intentionally separate from the
    /// <see cref="FontRole.PacketLabel"/> entry in <see cref="Roles"/>, which is a measurement spec
    /// the label never consumes (packet labels render un-measured). A style customises packet-label
    /// type here.
    /// </summary>
    public required FontRoleSpec PacketLabel { get; init; }

    /// <summary>
    /// When <c>true</c> (editorial's textbook-figure identity), the narration caption bar renders as a
    /// numbered figure caption: each beat's text is prefixed with a deterministic <c>Fig. N — </c>
    /// (<c>N</c> is the 1-based beat order, derived from content so it is stable across renders) and
    /// the caption text is set in serif <em>italic</em>. The prefix is prepended <em>before</em> the
    /// narration word-wrap/measurement, so the measured and rendered strings stay identical (no
    /// <c>textLength</c> desync — narration text is centred and un-guarded either way). <c>false</c>
    /// (classic, and every style that doesn't set it) emits the caption verbatim and upright —
    /// byte-identical.
    /// </summary>
    public bool NarrationFigureCaption { get; init; }

    /// <summary>
    /// A string prepended to every primary node title (card/pill/class/ghost) — terminal's
    /// <c>[bracketed]</c> label affordance is <c>TitlePrefix = "["</c>, <c>TitleSuffix = "]"</c>. The
    /// decoration is applied by <see cref="DecorateTitle"/> at both the <em>measurement</em> boundary
    /// (<c>CardSizer</c> sizes the box for the bracketed run) and the render boundary (the same
    /// bracketed run is drawn and word-wrapped), so the measured box and the rendered <c>textLength</c>
    /// guard stay matched — the brackets widen the card, they never overflow it. Subtitles, status
    /// pills, the diagram title/subtitle, edge/group/sequence labels, and class members are left
    /// undecorated. <c>""</c> (classic, and every style that doesn't set it) prepends/appends nothing —
    /// byte-identical. The prefix/suffix are emitted as <c>&lt;text&gt;</c> content (XML-escaped), never
    /// into the <c>&lt;style&gt;</c> block, so they carry no CSS-injection surface.
    /// </summary>
    public string TitlePrefix { get; init; } = "";

    /// <summary>The string appended to every primary node title; see <see cref="TitlePrefix"/>.</summary>
    public string TitleSuffix { get; init; } = "";

    /// <summary>
    /// Wrap a node title in <see cref="TitlePrefix"/>/<see cref="TitleSuffix"/> (terminal's brackets).
    /// A no-op returning <paramref name="title"/> unchanged when neither is set (classic — so the
    /// measured and rendered strings are byte-identical to today). Called at every title measurement
    /// <em>and</em> render site so the two stay in lockstep.
    /// </summary>
    public string DecorateTitle(string title) =>
        TitlePrefix.Length == 0 && TitleSuffix.Length == 0 ? title : TitlePrefix + title + TitleSuffix;
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

    /// <summary>
    /// When <c>true</c>, <em>every</em> architecture/sequence edge carries <see cref="EdgeDash"/> on
    /// its base <c>.beck-edge</c> stroke by default (blueprint's technical-drawing dashed lines), not
    /// only edges the author marked <c>style: dashed</c>. Purely a stroke treatment — the edge stays
    /// one continuous <c>&lt;path&gt;</c>, so packets/trails (which ride that path via
    /// <c>offset-path</c>) and routing are untouched. <c>false</c> (classic, and every style that
    /// doesn't set it) leaves the base edge solid and byte-identical.
    /// </summary>
    public bool DashedEdges { get; init; }

    /// <summary>
    /// When <c>true</c> (glow's luminous edges), a single <c>&lt;linearGradient&gt;</c> — defined once
    /// in the diagram <c>&lt;defs&gt;</c> with an id scoped by the 8-char content hash
    /// (<c>beck-edge-grad-{hash}</c>) and stops built from <c>color-mix</c> over the <c>--beck-*</c>
    /// tokens (no resolved literals) — paints edges that use the <em>default</em> edge colour
    /// (<c>var(--beck-edge)</c>). Author-coloured edges keep their explicit colour, and every edge is
    /// still one continuous <c>&lt;path&gt;</c>, so packets/trails/markers and routing are untouched.
    /// <c>false</c> (classic, and every style that doesn't set it) emits no gradient and paints edges
    /// flat — byte-identical.
    /// </summary>
    public bool GradientEdges { get; init; }

    /// <summary>
    /// When <c>true</c> (glow's glass panels), every <c>.beck-node</c> surface (card/pill/class/ghost)
    /// strokes with a single <c>&lt;linearGradient&gt;</c> — defined once in the diagram
    /// <c>&lt;defs&gt;</c> with an id scoped by the 8-char content hash (<c>beck-node-grad-{hash}</c>)
    /// and stops built from the <c>--beck-node-grad-a</c>/<c>--beck-node-grad-b</c> tokens (falling back
    /// to <c>--beck-accent</c>/<c>--beck-info</c>) — instead of the flat accent-mix stroke. The gradient
    /// uses the default <c>objectBoundingBox</c> units, which is the correct and only way a <em>single
    /// shared</em> def can paint each node's own corner-to-corner cyan→violet rim: a node rect always has
    /// positive area, so — unlike the degenerate zero-area straight <em>edge</em> the userSpaceOnUse edge
    /// fix addresses — an objectBoundingBox gradient renders identically to a per-node userSpaceOnUse one
    /// while letting all nodes share one def (exactly the mock's single <c>gg-a</c> gradient). Internal
    /// dividers/head-borders keep their flat token stroke. <c>false</c> (classic, and every style that
    /// doesn't set it) emits no gradient and keeps the flat accent-mix node stroke — byte-identical.
    /// </summary>
    public bool GradientNodes { get; init; }
}

/// <summary>How a style draws an edge's <em>arrowhead</em> (<see cref="StyleEdges.Arrow"/>).</summary>
public enum EdgeArrow
{
    /// <summary>The classic filled/closed marker bodies (filled polygon arrow, closed triangle/diamond) —
    /// byte-identical to today. The marker fill is the edge's own token colour, so a style that recolours
    /// its edges already gets style-coloured fills for free.</summary>
    Filled,

    /// <summary>
    /// A hand-drawn <em>open V</em> arrowhead (sketch): the filled <see cref="Rendering.MarkerShape.Arrow"/> is
    /// replaced by <em>two</em> short round-capped strokes running back from the tip, so the arrow reads
    /// as two pen strokes rather than a solid triangle. Closed UML ends (inheritance triangle, composition
    /// diamond) intentionally keep their bodies — only the plain arrowhead opens up.
    /// </summary>
    OpenV,

    /// <summary>
    /// A mono <c>&gt;</c> <em>chevron</em> arrowhead (terminal): the plain arrowheads
    /// (<see cref="Rendering.MarkerShape.Arrow"/> and the open <see cref="Rendering.MarkerShape.ArrowOpen"/>)
    /// become <em>two hard butt-capped strokes</em> forming a crisp <c>&gt;</c> chevron — the deterministic,
    /// measurable equivalent of the mock's mono <c>&gt;</c> text glyph, which stays crisp at classic marker
    /// sizes and scales with <see cref="StyleEdges.MarkerScale"/> / <see cref="StyleEdges.MarkerScaleToWidth"/>.
    /// The chevron is oriented along the edge direction, so a reply message's reversed path yields the
    /// <c>&lt;</c> read for free through the marker's <c>orient="auto-start-reverse"</c>. Closed UML ends
    /// (inheritance triangle, composition diamond) intentionally keep their bodies — only the plain
    /// arrowhead becomes a chevron.
    /// </summary>
    Chevron,
}

/// <summary>The optional <em>overlay layer</em> a style rides on top of every edge
/// (<see cref="StyleEdges.Overlay"/>) — an additional element sharing the edge's exact <c>d</c>.</summary>
public enum EdgeOverlay
{
    /// <summary>No overlay — classic. The edge is a single continuous <c>&lt;path&gt;</c> and nothing else.</summary>
    None,

    /// <summary>A short bright <em>comet</em> dash that travels the edge continuously (glow's luminous
    /// connectors): a second path sharing the edge's <c>d</c> with a
    /// <c>stroke-dasharray:{CometDash} {pathLength}</c> window whose <c>stroke-dashoffset</c> sweeps one
    /// dot end-to-end each <see cref="StyleEdges.OverlayPeriod"/>, phased per edge by a baked dash-offset
    /// (content-hash derived) — never an <c>animation-delay</c> chain.</summary>
    Comet,

    /// <summary>A <em>draw-on</em> overlay: the edge redraws itself once per
    /// <see cref="StyleEdges.OverlayPeriod"/> (sketch's self-drawing connectors) via a
    /// <c>stroke-dasharray:{len} {len}</c> whose offset wipes from hidden to fully drawn, holds, then
    /// resets — all baked keyframe stops, no delay chain.</summary>
    DrawOn,

    /// <summary>A <em>marching-ants</em> overlay: a repeating dash pattern that flows along the edge
    /// continuously (a per-edge cousin of the flow <c>stream</c> effect).</summary>
    Marching,
}

/// <summary>How a style paints sequence <em>lifelines</em> and class compartment separators
/// (<see cref="StyleEdges.Lifeline"/> / <see cref="StyleEdges.WobblySeparators"/>).</summary>
public enum LifelineShape
{
    /// <summary>A straight dashed line (classic) — <c>&lt;line&gt;</c> + <see cref="StyleStrokes.LifelineDash"/>.</summary>
    Dashed,

    /// <summary>A straight <em>solid</em> line with no dash (glow's faint solid lifelines).</summary>
    FaintSolid,

    /// <summary>A single subtle sideways <em>bow</em> (sketch's hand-drawn lifeline) — one continuous
    /// <c>&lt;path&gt;</c> whose two endpoints match the straight line's, jittered from the content hash.</summary>
    Wobbly,
}

/// <summary>
/// The per-style edge-presentation seam: base-layer treatment, an optional overlay layer sharing the
/// edge's <c>d</c>, arrowhead presentation + marker scaling, deterministic path bow shaping, and the
/// lifeline/separator treatment. Every field defaults to the classic value, so
/// <see cref="Classic"/> and any style that leaves <see cref="BeckStyle.Edges"/> unset render
/// byte-identically to today. Data only — the emitters and the overlay compiler branch on it.
/// </summary>
public sealed record StyleEdges
{
    // ---- base layer ----
    /// <summary>The base edge stroke's <c>stroke-linecap</c> (the <c>.beck-edge</c> CSS). <c>round</c>
    /// (classic) is byte-identical; a hard-edged style can pick <c>butt</c> or <c>square</c>.</summary>
    public string BaseLinecap { get; init; } = "round";

    /// <summary>Optional base-edge <c>stroke-opacity</c> (glow's faint slate base layer). <c>null</c>
    /// (classic) emits no attribute — byte-identical; a value in [0,1] fades the base stroke so an
    /// overlay/comet reads as the bright layer over a dim rail.</summary>
    public double? BaseOpacity { get; init; }

    /// <summary>
    /// A per-index colour <em>palette</em> that cycles the <em>base</em> edge stroke — metro's headline
    /// "each relationship is its own transit-line colour" (the cousin of <see cref="OverlayPalette"/>,
    /// which cycles the travelling <em>overlay</em> hue; this one recolours the static base line itself).
    /// Each entry is a CSS colour, normally a token expression (<c>var(--beck-line-1)</c>) so it
    /// theme-adapts and never emits a resolved literal. Empty (classic, and every style that doesn't set
    /// it) leaves the base edge on its single <c>var(--beck-edge)</c> token — byte-identical.
    /// <para><b>Cycle (deterministic, stable index — never the content hash).</b> The palette is indexed
    /// by each element's own stable draw order (architecture edge index = <c>model.Edges</c> order;
    /// sequence participant index = participant-column order), so <c>palette[i % Count]</c> — a fixed,
    /// reproducible assignment, not an RNG/hash phase.</para>
    /// <para><b>What it recolours, and coherence.</b> On the architecture side it recolours an edge's base
    /// stroke, its arrowhead marker, and its metro station-dot rings together, so the whole transit line
    /// (line + heads + stations) reads one hue. On the sequence side it recolours each participant's
    /// <em>lifeline</em> by participant-column index, and each <em>message</em> (line + its two endpoint
    /// station dots) by its <b>source participant</b> (<c>edge.From</c>) — the coherent rule chosen after
    /// studying the mock's metro sequence panel (per-participant coloured vertical lifelines, with each
    /// horizontal hop reading in the colour of the line it departs). The flow <em>packet</em> keeps its own
    /// colour (the mock's train dash is a single neutral hue riding every line), so packet colouring is
    /// unchanged — the requirement that station rings / packet colours which already derive from the
    /// per-edge accent keep working is met because this field never touches an author-coloured element.</para>
    /// <para><b>Precedence — an explicit accent WINS.</b> The palette only paints elements that use their
    /// <em>default</em> colour: an architecture edge whose colour is the default <c>var(--beck-edge)</c>
    /// (an author's explicit per-edge accent, or a kind default like a dependency's <c>--beck-neutral</c>,
    /// keeps its colour and its matching marker/stations); a sequence participant whose accent is its
    /// kind default (an explicit participant accent keeps its colour on its own lifeline <em>and</em> on
    /// the messages departing it, so line and hops stay one hue); and a message without an explicit
    /// <c>color:</c> (<see cref="Rendering.EdgeModel.ColorAuthored"/> — an authored colour always wins).</para>
    /// </summary>
    public IReadOnlyList<string> BaseColorPalette { get; init; } = System.Array.Empty<string>();

    /// <summary>
    /// The palette entry at a stable draw-order <paramref name="index"/> — the single cycle
    /// implementation shared by <see cref="BaseColorPalette"/> (via <see cref="PaletteHue"/>) and
    /// <see cref="OverlayPalette"/>. Negative-safe modulo; every current caller passes a 0-seeded
    /// draw-order counter, but the guard lives here — exactly once — so a future negative phase
    /// can't silently diverge between the two palettes.
    /// </summary>
    internal static string Cycle(IReadOnlyList<string> palette, int index) =>
        palette[((index % palette.Count) + palette.Count) % palette.Count];

    /// <summary>
    /// The palette hue at a stable <paramref name="index"/> (<see cref="Cycle"/>), or <c>null</c> when
    /// <see cref="BaseColorPalette"/> is empty. Eligibility ("does this element use its default
    /// colour?") is decided by each call site, so this is purely the cycle.
    /// </summary>
    public string? PaletteHue(int index) =>
        BaseColorPalette.Count == 0 ? null : Cycle(BaseColorPalette, index);

    /// <summary>
    /// The effective base colour for an architecture/class edge: its palette hue when
    /// <see cref="BaseColorPalette"/> is set <em>and</em> the edge uses the default colour
    /// (<see cref="Rendering.Defaults.EdgeColor"/>) — otherwise the edge's own
    /// <paramref name="edgeColor"/> unchanged, so an author's explicit accent wins. Byte-inert
    /// (returns <paramref name="edgeColor"/>) when the palette is empty.
    /// </summary>
    public string BaseColorFor(int index, string edgeColor) =>
        BaseColorPalette.Count > 0 && edgeColor == Rendering.Defaults.EdgeColor
            ? PaletteHue(index)!
            : edgeColor;

    // ---- underlay layer (static trace bed) ----
    /// <summary>
    /// The stroke width (px) of a static <em>trace-bed underlay</em> drawn <em>behind</em> the base edge
    /// (circuit's signature two-layer trace): a second, wider, darker <c>&lt;path&gt;</c> sharing the
    /// edge's exact <c>d</c>, emitted first in document order so the thin bright <c>.beck-edge</c> line
    /// reads as a trace riding a dark bed. Unlike <see cref="Overlay"/> (always the travelling element)
    /// the bed is <em>static</em> — no animation, no reduced-motion gate — and the base edge stays the one
    /// continuous flow path packets/trails ride via <c>offset-path</c>; the bed is an additional sibling
    /// element, never a split. Applies to architecture/class edges <em>and</em> sequence messages +
    /// lifelines (the mock beds those too). <c>0</c> (classic, and every style that doesn't set it) emits
    /// no bed — byte-identical. Typical circuit value is ~2× the base <see cref="StyleGeometry.EdgeStroke"/>.
    /// </summary>
    public double UnderlayWidth { get; init; }

    /// <summary>
    /// The trace-bed underlay's stroke colour, in play only when <see cref="UnderlayWidth"/> &gt; 0. A CSS
    /// colour, normally a token expression / <c>color-mix</c> so it theme-adapts and never emits a resolved
    /// literal (e.g. a darker mix of <c>--beck-edge</c>). <c>""</c> (the default) falls back to
    /// <c>var(--beck-edge-underlay, var(--beck-edge))</c>, so a style can either supply a dedicated bed
    /// token or let the bed track the edge colour. Coherent with the palette-less
    /// <c>var(--beck-edge-overlay, …)</c> fallback used by the overlay layer.
    /// </summary>
    public string UnderlayColor { get; init; } = "";

    // ---- arrowhead ----
    /// <summary>The arrowhead presentation (<see cref="EdgeArrow.Filled"/> classic, <see cref="EdgeArrow.OpenV"/>
    /// hand-drawn strokes, <see cref="EdgeArrow.Chevron"/> mono <c>&gt;</c>). <see cref="EdgeArrow.Filled"/>
    /// (classic) is byte-identical.</summary>
    public EdgeArrow Arrow { get; init; } = EdgeArrow.Filled;

    /// <summary>A multiplier on the emitted marker geometry. <c>1.0</c> (classic) is byte-identical.
    /// With <see cref="MarkerScaleToWidth"/> off it scales the marker in the default strokeWidth units;
    /// with it on it scales the absolute (userSpaceOnUse) marker size.</summary>
    public double MarkerScale { get; init; } = 1.0;

    /// <summary>
    /// An optional override colour for the edge's arrowhead marker (glow's comet-coloured filled
    /// triangles): when set <em>and</em> the edge uses the default colour (<c>var(--beck-edge)</c>), the
    /// marker is drawn in this colour instead of the edge's own faint-slate stroke, so a dim base rail can
    /// carry a bright comet-hued arrowhead. Author-coloured edges keep their explicit colour + matching
    /// marker. A CSS colour, normally a token expression (<c>var(--beck-comet-2)</c>). <c>null</c>
    /// (classic, and every style that doesn't set it) leaves markers on the edge's own colour —
    /// byte-identical.
    /// </summary>
    public string? MarkerColor { get; init; }

    /// <summary>
    /// When <c>true</c>, markers switch from the SVG default <c>markerUnits="strokeWidth"</c> (where a
    /// marker's size multiplies by the edge's stroke-width — so a thick metro/brutalist edge blows the
    /// arrowhead up into a blob) to <c>markerUnits="userSpaceOnUse"</c> with an <em>absolute</em> size
    /// that grows only <em>sub-linearly</em> with the edge stroke width (<c>base · MarkerScale ·
    /// √width</c>). This is the seam the metro/brutalist juries wanted: the arrowhead stays sanely sized
    /// on a thick line. <c>false</c> (classic) keeps the historical marker element verbatim — byte-identical.
    /// </summary>
    public bool MarkerScaleToWidth { get; init; }

    /// <summary>
    /// An optional <em>contrast outline</em> stroke drawn around the plain filled arrowhead
    /// (<see cref="Rendering.MarkerShape.Arrow"/> under <see cref="EdgeArrow.Filled"/>) — brutalist's lime
    /// arrowhead with a white <c>1.5</c> outline. When set, the filled arrow polygon gains
    /// <c>stroke:{MarkerOutline};stroke-width:1.5</c> and the marker element is drawn
    /// <c>overflow="visible"</c> so the outline is not clipped by the marker viewport. A CSS colour, normally
    /// a token expression (<c>var(--beck-edge)</c>) so it theme-adapts and never emits a resolved literal.
    /// Only the plain filled arrowhead is outlined — the open-V / chevron treatments and the closed UML ends
    /// (which already carry their own stroke) are untouched. <c>null</c> (classic, and every style that
    /// doesn't set it) leaves the arrowhead a flat single-colour fill — byte-identical.
    /// </summary>
    public string? MarkerOutline { get; init; }

    // ---- overlay layer ----
    /// <summary>The overlay treatment sharing each edge's <c>d</c>. <see cref="EdgeOverlay.None"/>
    /// (classic) emits no overlay — byte-identical.</summary>
    public EdgeOverlay Overlay { get; init; } = EdgeOverlay.None;

    /// <summary>The overlay path's stroke width (only in play when <see cref="Overlay"/> ≠ None).</summary>
    public double OverlayWidth { get; init; } = 2.5;

    /// <summary>
    /// The per-edge overlay <em>colour palette</em> (glow's cyan / light-cyan / violet comets): the
    /// overlay path on edge <c>i</c> takes <c>OverlayPalette[i % Count]</c>, so consecutive edges/messages
    /// alternate hues deterministically (index is the edge's own draw order — no RNG). Each entry is a CSS
    /// colour, normally a token expression (<c>var(--beck-comet-1)</c>). Empty (classic, and every style
    /// that leaves it unset — including a DrawOn/Marching overlay style like sketch) falls back to the
    /// single <c>var(--beck-edge-overlay, var(--beck-accent))</c> token — byte-identical.
    /// </summary>
    public IReadOnlyList<string> OverlayPalette { get; init; } = System.Array.Empty<string>();

    /// <summary>
    /// An optional CSS <c>filter</c> applied to the overlay path only (glow's comet bloom): a
    /// <c>drop-shadow(…)</c> that haloes the travelling comet without blooming the faint base rail or the
    /// diagram's labels (the mock blooms the whole edge group; blooming just the bright overlay is the
    /// clean equivalent and keeps text crisp). Keep the colour in a <c>--beck-*</c> token /
    /// <c>color-mix</c> so it theme-adapts. <c>""</c> (classic, and every style that doesn't set it) emits
    /// no filter — byte-identical.
    /// </summary>
    public string OverlayBloom { get; init; } = "";

    /// <summary>The overlay path's <c>stroke-linecap</c> (round makes the comet a rounded pill).</summary>
    public string OverlayLinecap { get; init; } = "round";

    /// <summary>The lit dash length (px) of the comet / marching overlay.</summary>
    public double CometDash { get; init; } = 10;

    /// <summary>Seconds for one overlay cycle: a comet traverses the whole edge once, a draw-on redraws
    /// once, marching ants advance one dash pattern. A continuous <c>linear infinite</c> loop (like the
    /// existing flow <c>stream</c> march), compiled — never a per-element delay chain.</summary>
    public double OverlayPeriod { get; init; } = 2.2;

    /// <summary>
    /// The overlay animation's <em>timing function</em>. <c>null</c> (classic, and every style that leaves
    /// it unset) emits <c>linear</c> — the smooth glide of glow's comet / a marching dash — byte-identical.
    /// A non-null <c>n</c> emits <c>steps(n)</c> instead, so the Comet / Marching overlay advances in
    /// <c>n</c> hard discrete jumps per cycle rather than gliding: the mechanical tick that <em>is</em> the
    /// identity for brutalist (a lime block ratcheting each edge) and terminal (a phosphor block stepping
    /// down each wire). Extends the existing stepped-flow discipline (<see cref="StyleMotion.PacketSteps"/> /
    /// <see cref="StyleMotion.TrailSteps"/>) to the ambient edge overlay. Applies to the whole overlay set (all edges
    /// share one compiled cycle); ignored when <see cref="Overlay"/> is <see cref="EdgeOverlay.None"/> or
    /// <see cref="EdgeOverlay.DrawOn"/> (draw-on's eased wipe reads as a smooth ink, not a tick).
    /// </summary>
    public int? OverlaySteps { get; init; }

    // ---- path shaping ----
    /// <summary>
    /// The amplitude (px) of a deterministic quadratic <em>bow</em> applied to every straight run of the
    /// emitted edge path at the SVG layer (sketch's hand-drawn connectors): each segment between route
    /// anchors bows through a perpendicular-displaced midpoint, its sign/size derived from the content
    /// hash, with the endpoints and every elbow anchor preserved and the edge still <em>one</em>
    /// continuous path. <c>0</c> (classic) leaves the router's path verbatim — byte-identical.
    /// </summary>
    public double BowAmplitude { get; init; }

    // ---- lifelines / separators ----
    /// <summary>The sequence lifeline treatment. <see cref="LifelineShape.Dashed"/> (classic) is byte-identical.</summary>
    public LifelineShape Lifeline { get; init; } = LifelineShape.Dashed;

    /// <summary>When <c>true</c> (sketch), class compartment separators (the header rule + between-section
    /// dividers) are drawn as subtle wobbly <c>&lt;path&gt;</c>s instead of straight <c>&lt;line&gt;</c>s,
    /// their endpoints preserved and jitter hash-derived. <c>false</c> (classic) keeps straight lines —
    /// byte-identical.</summary>
    public bool WobblySeparators { get; init; }

    /// <summary>The classic reference: every knob at its historical value (byte-identical output).</summary>
    public static StyleEdges Classic { get; } = new();
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
    /// <summary>Whether a packet's authored <c>glow: true</c> applies the Gaussian-blur filter.
    /// Off styles still render the packet dot/ring itself — only the decorative bloom is
    /// suppressed, so the packet feature stays fully available.</summary>
    public required bool GlowEnabled { get; init; }

    /// <summary>
    /// The <c>stdDeviation</c> of the packet-bloom Gaussian blur (<c>beck-glow-{hash}</c> filter) —
    /// how far a glowing packet dot's halo spreads. Only in play when <see cref="GlowEnabled"/> is
    /// <c>true</c> and a packet actually glows; a larger value leans harder into the packet dot
    /// (glow's showpiece element). <c>3.0</c> (classic, and every style that doesn't set it)
    /// reproduces the exact historical filter — byte-identical.
    /// </summary>
    public double PacketGlowBlur { get; init; } = 3.0;

    /// <summary>
    /// Whether the decorative <em>impact</em> (expanding ring at a packet's landing) and
    /// <em>working</em> (breathing ring around a busy card) ring overlays render at all. Unlike
    /// <see cref="EffectAmplitude"/> — which only scales their peak opacity/stroke and so never fully
    /// removes them — this is a hard gate: <c>false</c> (minimal's "rings off" identity) emits neither
    /// the ring markup nor its keyframes, so a restrained style shows no hollow rings while its
    /// packets, trails, pulses, highlights, and status pills all still animate. The underlying
    /// <c>impact</c>/<c>working</c> flow features stay authorable; only their ring decoration is
    /// suppressed. <c>true</c> (classic, and every style that doesn't set it) renders both rings
    /// exactly as before — byte-identical.
    /// </summary>
    public bool RingsEnabled { get; init; } = true;
    /// <summary>Uniform amplitude multiplier (0–1 typical) on the peak opacity/stroke-width of the
    /// ripple, highlight/fail border-glow, impact-ring, and working-ring effects — a single dial for
    /// "motion stays but understated" styles. <c>1.0</c> reproduces classic's exact peaks.</summary>
    public required double EffectAmplitude { get; init; }

    /// <summary>
    /// The default packet glyph a style prefers when the author's flow step doesn't request one —
    /// a third fallback tier under the author's explicit <c>packet.shape</c>: <c>k.Shape ??
    /// style.Motion.PacketGlyph ?? Dot</c> (mapped through <see cref="PacketGlyph"/> at the schedule
    /// boundary, since a style-level default may pick a glyph no individual packet step can yet
    /// author, e.g. <see cref="Beck.PacketGlyph.Square"/>). <c>null</c> (classic, and every style
    /// that doesn't set this) leaves the existing two-tier <c>k.Shape ?? Dot</c> resolution untouched.
    /// </summary>
    public PacketGlyph? PacketGlyph { get; init; }

    /// <summary>
    /// When set, the packet <em>trail</em>'s reveal (the <c>beck-trail</c> stroke-dashoffset track)
    /// uses a <c>steps(n)</c> timing function instead of the packet's own ease — a blocky, hard-cut
    /// reveal ("hard-step trails", terminal's identity) instead of a smooth wipe. The travelling
    /// packet glyph keeps its normal per-edge-kind ease; only the trail reveal steps. <c>null</c>
    /// (classic) leaves the trail using the packet's ease, unchanged.
    /// </summary>
    public int? TrailSteps { get; init; }

    /// <summary>
    /// When set, the travelling packet glyph's own advance along its edge (the <c>offset-distance</c>
    /// track) uses a <c>steps(n)</c> timing function instead of its per-edge-kind ease — the packet
    /// hops the edge in <c>n</c> discrete, mechanical jumps ("stepped flow motion", brutalist's
    /// identity), extending the terminal <see cref="TrailSteps"/> trail-only seam to the moving glyph
    /// itself. When <see cref="TrailSteps"/> is unset the trail reveal follows this stepped ease too
    /// (they share the packet's timing function), so a style can hard-step both from one knob. Only
    /// the flow effect steps — nothing animates at rest. <c>null</c> (classic, and every style that
    /// doesn't set it) leaves the packet on its smooth per-edge-kind ease, unchanged.
    /// </summary>
    public int? PacketSteps { get; init; }

    /// <summary>
    /// Multiplier on the sequence-storytelling reveal ramp durations — the fade-in windows that
    /// draw each message row, chip, and section band up from its dimmed state as the flow reaches it
    /// (<c>CssCompiler.SequenceChoreoCss</c>). A value <c>&gt; 1</c> stretches those windows so the
    /// scenery draws on slowly and softly (editorial's textbook-figure reveal) — the <em>existing</em>
    /// sequence-reveal choreography with a longer ease, not a new mechanism. Only the reveal ramp
    /// lengthens; the initial dims, hold, and finale are unchanged, and the whole track stays compiled
    /// onto the shared cycle (no <c>animation-delay</c>). <c>1.0</c> (classic, and every style that
    /// doesn't set it) reproduces the exact historical 0.25s/0.4s ramps — byte-identical.
    /// </summary>
    public double SequenceRevealScale { get; init; } = 1.0;

    /// <summary>
    /// When <c>true</c> (extrude's identity), a node's active-effect transform (pulse / highlight
    /// peak) <em>presses down toward its depth faces</em> — a small <c>translate(2px,2px)</c> dip
    /// instead of the classic <c>translateY(-2px) scale(1.04)</c> lift — so a 2.5D slab reads as
    /// being pushed into the page rather than floating up. Compiled into the same shared-cycle
    /// transform keyframes (no <c>animation-delay</c>); the fail shake is orthogonal and unchanged.
    /// <c>false</c> (classic, and every style that doesn't set it) keeps the historical lift —
    /// byte-identical.
    /// </summary>
    public bool PressDown { get; init; }
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
    /// <summary>Narration-bar drop-shadow filter (a CSS <c>filter</c> value, e.g. <c>none</c> to
    /// turn it off).</summary>
    public required string NarrationShadow { get; init; }

    /// <summary>
    /// The gap (px) between the narration caption's leading bullet dot and its text. An emitter
    /// literal promoted to a style field so a wider-set style (terminal's mono caption, where the
    /// dot otherwise crowds the first glyph) can give the bullet more air without touching the
    /// bullet radius or the caption's centred layout. <c>9.6</c> (classic, and every style that
    /// doesn't set it) reproduces the historical spacing — byte-identical.
    /// </summary>
    public double NarrationBulletGap { get; init; } = 9.6;

    /// <summary>
    /// Extra CSS declarations painted onto the diagram's root <c>&lt;svg&gt;</c> box (the scope
    /// selector) — blueprint's faint graph-paper grid via a token-driven <c>background-image</c>. The
    /// value is a raw declaration list (e.g. <c>background-image:…;background-size:…;</c>) appended
    /// inside the root rule after <c>font-family</c>; keep colours in <c>var(--beck-*)</c> tokens so it
    /// theme-adapts and never emits a resolved literal into shape CSS. <c>""</c> (classic, and every
    /// style that doesn't set it) emits nothing and stays byte-identical.
    /// </summary>
    public string SurfaceBackground { get; init; } = "";

    /// <summary>
    /// The down-right offset (px) of the neo-brutalist hard shadow rect drawn behind each card/pill/
    /// class node under <see cref="StyleArtwork.Brutalist"/>. Only consumed when the style's
    /// <see cref="BeckStyle.Artwork"/> is <see cref="StyleArtwork.Brutalist"/>; a <c>0</c> (classic,
    /// and every non-brutalist style) emits no shadow element — byte-identical. Typical brutalist
    /// value is 4–6.
    /// </summary>
    public double ShadowOffset { get; init; } = 0;

    /// <summary>
    /// The down-right offset (px) of the two 2.5D depth faces drawn behind each card/pill/class node
    /// under <see cref="StyleArtwork.Extruded"/> — the apparent slab thickness, with the light source
    /// read as top-left. Only consumed when the style's <see cref="BeckStyle.Artwork"/> is
    /// <see cref="StyleArtwork.Extruded"/>; a <c>0</c> (classic, and every non-extrude style) emits no
    /// faces — byte-identical. Typical extrude value is 6-8.
    /// </summary>
    public double DepthOffset { get; init; } = 0;

    /// <summary>
    /// The length (px) a decorative chip pin stub protrudes past a card/pill/class node's left/right
    /// edge under <see cref="StyleArtwork.Circuit"/>. Only consumed when the style's
    /// <see cref="BeckStyle.Artwork"/> is <see cref="StyleArtwork.Circuit"/>; a <c>0</c> (classic, and
    /// every non-circuit style) emits no pins — byte-identical. Typical circuit value is 5-7.
    /// </summary>
    public double PinLength { get; init; } = 0;

    /// <summary>The thickness (px) of a circuit chip pin stub (its short dimension). Only in play under
    /// <see cref="StyleArtwork.Circuit"/> with a non-zero <see cref="PinLength"/>.</summary>
    public double PinThickness { get; init; } = 2.4;

    /// <summary>The nominal vertical spacing (px) between adjacent circuit chip pins: the node's pin
    /// count per side is <c>clamp(round(height / PinPitch), 2, 6)</c>, so a taller chip grows more pins
    /// deterministically. Only in play under <see cref="StyleArtwork.Circuit"/>.</summary>
    public double PinPitch { get; init; } = 24;

    /// <summary>The radius (px) of a circuit via dot dropped at each edge-route bend under
    /// <see cref="StyleArtwork.Circuit"/>. Only consumed when the style's <see cref="BeckStyle.Artwork"/>
    /// is <see cref="StyleArtwork.Circuit"/>; every other style emits no vias — byte-identical.</summary>
    public double ViaRadius { get; init; } = 2.6;

    /// <summary>The radius (px) of a metro station dot dropped at each edge's two anchor endpoints under
    /// <see cref="StyleArtwork.Metro"/>. Only consumed when the style's <see cref="BeckStyle.Artwork"/>
    /// is <see cref="StyleArtwork.Metro"/>; a <c>0</c> (classic, and every non-metro style) emits no
    /// stations — byte-identical. Typical metro value is 4-5 (large enough that its white fill shows
    /// past the thick line stroke).</summary>
    public double StationRadius { get; init; } = 0;

    /// <summary>The ring stroke width (px) of a metro station dot. Only in play under
    /// <see cref="StyleArtwork.Metro"/> with a non-zero <see cref="StationRadius"/>.</summary>
    public double StationRing { get; init; } = 2;

    /// <summary>
    /// The offset (px) of a blueprint group-box <em>dimension line</em> above the group's top edge, and
    /// the length of the two perpendicular witness ticks that join it back to the box corners. Only
    /// consumed when the style's <see cref="BeckStyle.Artwork"/> is <see cref="StyleArtwork.Blueprint"/>;
    /// a <c>0</c> (classic, and every non-blueprint style) emits no dimension lines — byte-identical.
    /// Typical blueprint value is 8-10 (small enough that the annotation stays clear of the group's own
    /// on-edge label and never runs off the always-positive canvas above the well-margined group boxes).
    /// </summary>
    public double DimensionTick { get; init; } = 0;

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
