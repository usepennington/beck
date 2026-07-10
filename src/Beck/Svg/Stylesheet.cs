using System.Text;
using Beck.Text;

namespace Beck.Svg;

/// <summary>
/// Emits the scoped <c>&lt;style&gt;</c> block: the <c>--beck-*</c> token cascade
/// (three-tier: token → host <c>--color-*</c> → literal fallback) from
/// <c>styles.css</c>, plus the SVG translation of the shape rules. Every selector
/// is prefixed <c>.b-{hash}</c> so multiple diagrams on one page never collide;
/// light/dark is purely token redefinition (§8.2).
/// </summary>
internal static class Stylesheet
{
    private static string Sw(double n) => SvgWriter.Num(n);
    private static string P(int n) => SvgWriter.Int(n);

    public static string Emit(string h, string fontFamily, string monoFamily, ThemeMode theme, BeckStyle style)
    {
        var sb = new StringBuilder();
        string scope = $".b-{h}";
        StyleGeometry geo = style.Geometry;
        StyleMix mix = style.Mix;
        StyleStrokes strokes = style.Strokes;

        // ---- stratum 1: tokens ----
        void Block(string selector, IReadOnlyList<(string Name, string Value)> tokens)
        {
            sb.Append(selector).Append('{');
            foreach (var (name, value) in tokens) sb.Append(name).Append(':').Append(value).Append(';');
            sb.Append("--beck-font:").Append(fontFamily).Append(';');
            sb.Append("--beck-font-mono:").Append(monoFamily).Append(';');
            sb.Append('}');
        }

        var light = style.LightTokens.Entries;
        var dark = style.DarkTokens.Entries;
        switch (theme)
        {
            case ThemeMode.Light:
                Block(scope, light);
                break;
            case ThemeMode.Dark:
                Block(scope, light);
                Block(scope, dark);
                break;
            default: // auto — both host-controlled and standalone hooks
                Block(scope, light);
                Block($"[data-theme='dark'] {scope}", dark);
                sb.Append("@media (prefers-color-scheme: dark){");
                Block($":root:not([data-theme='light']) {scope}", dark);
                sb.Append('}');
                break;
        }

        // ---- stratum 2: shape CSS (SVG translation of styles.css) ----
        // The root rule carries the diagram font and, for styles that opt in, a token-driven surface
        // background (blueprint's grid). Classic's SurfaceBackground is "" → byte-identical output.
        sb.Append(scope).Append("{font-family:var(--beck-font);");
        if (geo.SurfaceBackground.Length > 0) sb.Append(geo.SurfaceBackground);
        sb.Append('}');
        // fx-node wrapper: effect transforms (scale/shake) pivot on the card centre.
        sb.Append($"{scope} .beck-fx-node{{transform-box:fill-box;transform-origin:center;}}");
        // Packet-label CSS (mono 11px 600) sourced from Typography.PacketLabel — deliberately
        // distinct from the FontRoles.PacketLabel measurement spec (sans 10.56); the label is never
        // measured, so the rendered type stands on its own style seam.
        var pl = style.Typography.PacketLabel;
        sb.Append($"{scope} .beck-packet-label{{font-family:{(pl.Mono ? "var(--beck-font-mono)" : "var(--beck-font)")};font-size:{Sw(pl.SizePx)}px;font-weight:{P(pl.Weight)};}}");

        // node card
        // Node stroke: the flat accent-mix rim (classic), or — when the style opts into gradient node
        // surfaces (glow's glass panels) — the single shared cyan→violet linearGradient defined in
        // StyleDefs. Only the outer node rects pick this up; internal dividers/head-borders keep their
        // flat token stroke below. Classic emits the identical mix rule → byte-identical.
        string nodeStroke = strokes.GradientNodes
            ? $"url(#beck-node-grad-{h})"
            : $"color-mix(in srgb, var(--beck-accent) {P(mix.NodeStroke)}%, var(--beck-node-border))";
        sb.Append($"{scope} .beck-node{{")
          .Append("fill:var(--beck-node-bg);")
          .Append($"stroke:{nodeStroke};")
          .Append($"stroke-width:{Sw(geo.NodeStroke)};")
          .Append($"filter:{geo.NodeShadow};")
          .Append("}");
        sb.Append($"[data-theme='dark'] {scope} .beck-node{{filter:{geo.NodeShadowDark};}}");
        sb.Append("@media (prefers-color-scheme: dark){");
        sb.Append($":root:not([data-theme='light']) {scope} .beck-node{{filter:{geo.NodeShadowDark};}}");
        sb.Append('}');
        sb.Append($"{scope} .beck-node--external{{stroke-dasharray:{strokes.NodeDash};}}");
        sb.Append($"{scope} .beck-node--subtle{{opacity:.72;}}");
        sb.Append($"{scope} .beck-node--ghost{{fill:transparent;stroke-dasharray:{strokes.NodeDash};filter:none;}}");

        // icon chip
        sb.Append($"{scope} .beck-icon-chip{{fill:color-mix(in srgb, var(--beck-accent) {P(mix.IconChip)}%, var(--beck-icon-bg));}}");
        sb.Append($"{scope} .beck-node--ghost .beck-icon-chip{{fill:transparent;}}");
        sb.Append($"{scope} .beck-icon{{color:var(--beck-accent);}}");

        // text
        sb.Append($"{scope} .beck-node-title{{fill:var(--beck-text);}}");
        sb.Append($"{scope} .beck-node-subtitle,{scope} .beck-ghost-label{{fill:var(--beck-text-muted);}}");
        sb.Append($"{scope} .beck-status-inline{{fill:var(--beck-accent);}}");

        // status pill
        sb.Append($"{scope} .beck-status-bg{{fill:color-mix(in srgb, var(--beck-accent) {P(mix.StatusPill)}%, transparent);}}");
        sb.Append($"{scope} .beck-status-text{{fill:var(--beck-accent);}}");

        // group
        sb.Append($"{scope} .beck-group{{fill:none;stroke:var(--beck-group-border);stroke-width:{Sw(geo.GroupStroke)};stroke-dasharray:{strokes.GroupDash};}}");
        sb.Append($"{scope} .beck-group-label-bg{{fill:var(--beck-surface);}}");
        // Group labels are already uppercased at the render site; a style can additionally make them
        // mono (blueprint). Classic's GroupLabel role is sans → nothing appended → byte-identical.
        string glFamily = style.Typography.Roles.Of(FontRole.GroupLabel).Mono ? "font-family:var(--beck-font-mono);" : "";
        sb.Append($"{scope} .beck-group-label{{fill:var(--beck-group-label);{glFamily}}}");

        // state pills reuse the card treatment; start/end pseudo-states
        sb.Append($"{scope} .beck-node--start{{fill:var(--beck-text-muted);}}");
        sb.Append($"{scope} .beck-node--end{{fill:none;stroke:var(--beck-text-muted);stroke-width:{Sw(geo.EndNodeStroke)};}}");
        sb.Append($"{scope} .beck-end-dot{{fill:var(--beck-text-muted);}}");

        // class compartment card
        sb.Append($"{scope} .beck-class-head{{fill:color-mix(in srgb, var(--beck-accent) {P(mix.ClassHead)}%, transparent);}}");
        sb.Append($"{scope} .beck-class-head-border{{stroke:color-mix(in srgb, var(--beck-accent) {P(mix.ClassHeadBorder)}%, var(--beck-node-border));stroke-width:{Sw(geo.HairlineStroke)};}}");
        sb.Append($"{scope} .beck-class-divider{{stroke:var(--beck-node-border);stroke-width:{Sw(geo.HairlineStroke)};}}");
        sb.Append($"{scope} .beck-class-stereo{{fill:var(--beck-text-muted);}}");
        sb.Append($"{scope} .beck-class-title{{fill:var(--beck-text);}}");
        sb.Append($"{scope} .beck-class-field{{fill:var(--beck-text-muted);}}");
        sb.Append($"{scope} .beck-class-method{{fill:var(--beck-text);}}");

        // sequence scenery
        // Lifeline dash: classic Dashed (and Wobbly) keep the dash; FaintSolid (glow) drops it. Classic
        // emits the identical rule — byte-identical.
        string llDash = style.Edges.Lifeline == LifelineShape.FaintSolid ? "" : $"stroke-dasharray:{strokes.LifelineDash};";
        sb.Append($"{scope} .beck-lifeline{{stroke-width:{Sw(geo.LifelineStroke)};{llDash}}}");
        sb.Append($"{scope} .beck-activation{{filter:drop-shadow(0 0 5px color-mix(in srgb, var(--beck-accent) {P(mix.ActivationGlow)}%, transparent));}}");
        sb.Append($"{scope} .beck-msg-chip{{fill:var(--beck-node-bg);stroke:color-mix(in srgb, var(--beck-accent) {P(mix.ChipStroke)}%, transparent);stroke-width:{Sw(geo.HairlineStroke)};}}");
        // Message labels honour the MsgText role's uppercase flag (blueprint's mono-uppercase
        // annotations, consistent with its edge/group/band labels). MsgText is a mono role, so
        // uppercasing is width-invariant — the measured chip still fits. Classic's MsgText is
        // non-uppercase → nothing appended → byte-identical.
        string mtCase = style.Typography.Roles.Of(FontRole.MsgText).Uppercase ? "text-transform:uppercase;" : "";
        sb.Append($"{scope} .beck-msg-text{{fill:color-mix(in srgb, var(--beck-accent) {P(mix.MsgText)}%, var(--beck-text));{mtCase}}}");
        sb.Append($"{scope} .beck-msg--reply .beck-msg-chip{{stroke:none;}}");
        sb.Append($"{scope} .beck-msg--reply .beck-msg-text,{scope} .beck-msg-text--bare{{fill:var(--beck-text-muted);}}");
        sb.Append($"{scope} .beck-band-box{{fill:color-mix(in srgb, var(--beck-accent) {P(mix.BandFill)}%, transparent);stroke:color-mix(in srgb, var(--beck-accent) {P(mix.BandStroke)}%, transparent);stroke-width:{Sw(geo.BandBoxStroke)};stroke-dasharray:{strokes.GroupDash};}}");
        sb.Append($"{scope} .beck-band-chip{{fill:var(--beck-surface);stroke:color-mix(in srgb, var(--beck-accent) {P(mix.ChipStroke)}%, transparent);stroke-width:{Sw(geo.HairlineStroke)};}}");
        sb.Append($"{scope} .beck-band-label{{fill:color-mix(in srgb, var(--beck-accent) {P(mix.BandLabel)}%, var(--beck-text));}}");

        // edges + labels
        // DashedEdges (blueprint) dashes every base edge; classic leaves it solid (byte-identical).
        string edgeDash = strokes.DashedEdges ? $"stroke-dasharray:{strokes.EdgeDash};" : "";
        sb.Append($"{scope} .beck-edge{{fill:none;stroke-width:{Sw(geo.EdgeStroke)};stroke-linecap:{style.Edges.BaseLinecap};stroke-linejoin:round;{edgeDash}}}");
        // Edge-label type honours the EdgeLabel role's family/case flags (mono uppercase for blueprint);
        // the textLength guard keeps the run inside its measured box, so this is layout-safe. Classic's
        // EdgeLabel is sans/non-uppercase → nothing appended → byte-identical.
        var elSpec = style.Typography.Roles.Of(FontRole.EdgeLabel);
        string elType = (elSpec.Mono ? "font-family:var(--beck-font-mono);" : "") + (elSpec.Uppercase ? "text-transform:uppercase;" : "");
        sb.Append($"{scope} .beck-edge-label{{fill:var(--beck-text-muted);paint-order:stroke;stroke:var(--beck-surface);stroke-width:{geo.EdgeLabelHalo};stroke-linejoin:round;{elType}}}");

        // title block
        sb.Append($"{scope} .beck-title{{fill:var(--beck-text);}}");
        sb.Append($"{scope} .beck-subtitle{{fill:var(--beck-text-muted);}}");

        // ---- sketch-specific colour overrides (brief §1b) ----
        // Node/class titles take the SAME colour as the node's stroke — which sketch sets to the node's
        // pure accent (mix.NodeStroke = 100), so an accent node gets accent-inked text and a neutral one
        // reads as pencil ink. Class compartment dividers pick up the accent too (the mock draws every
        // separator in the node's stroke colour). These are appended last so they win over the base rules
        // above; the whole block is gated on the Sketch artwork, so classic and every other style are
        // byte-identical (nothing appended).
        if (style.Artwork == StyleArtwork.Sketch)
        {
            sb.Append($"{scope} .beck-node-title{{fill:var(--beck-accent);}}");
            sb.Append($"{scope} .beck-class-title{{fill:var(--beck-accent);}}");
            sb.Append($"{scope} .beck-class-divider{{stroke:color-mix(in srgb, var(--beck-accent) {P(mix.ClassHeadBorder)}%, var(--beck-node-border));}}");
            // The crayon fill (Artwork.Scribble): accent wax, run through the crayon filter (StyleDefs)
            // for rough pressure edges + grainy holes, and translucent so the paper shows through.
            // stroke-width is per-path (it scales with the box), so it is NOT set here.
            sb.Append($"{scope} .beck-scribble{{fill:none;stroke:var(--beck-accent);stroke-linecap:round;stroke-linejoin:round;opacity:.34;filter:url(#beck-crayon-{h});}}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Style-scoped SVG <c>&lt;defs&gt;</c> content (concatenated after the markers + animation defs).
    /// <para>Glow's <em>node</em> gradient (<see cref="StyleStrokes.GradientNodes"/>): one shared
    /// <c>&lt;linearGradient&gt;</c> (id scoped by the content hash, stops built from
    /// <c>--beck-node-grad-a</c>/<c>--beck-node-grad-b</c> tokens — no resolved literals) painting every
    /// node's own corner-to-corner cyan→violet rim. This uses the default <c>objectBoundingBox</c> units
    /// deliberately: a node rect always has positive area, so a single objectBoundingBox def renders
    /// identically to a per-node userSpaceOnUse gradient while serving all nodes at once — unlike the
    /// degenerate zero-area straight <em>edge</em> case, where the gradient is emitted per-edge in
    /// <c>SvgRenderer.Edge</c> with <c>gradientUnits="userSpaceOnUse"</c> along the edge's own endpoints.</para>
    /// Returns <c>""</c> for every style that doesn't opt in (classic included), so the <c>&lt;defs&gt;</c>
    /// block stays byte-identical to classic.
    /// </summary>
    public static string StyleDefs(string h, BeckStyle style)
    {
        string defs = "";
        if (style.Strokes.GradientNodes)
            defs += $"<linearGradient id=\"beck-node-grad-{h}\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">" +
                    "<stop offset=\"0\" stop-color=\"var(--beck-node-grad-a, var(--beck-accent))\"/>" +
                    "<stop offset=\"1\" stop-color=\"var(--beck-node-grad-b, var(--beck-info))\"/>" +
                    "</linearGradient>";
        // Sketch's crayon-wax filter, applied to every .beck-scribble fill path: a low-frequency
        // turbulence displaces the stroke outline (wobbly hand-pressure edges), then a high-frequency
        // grain field turned into an alpha mask by the feColorMatrix eats waxy holes in the stroke so
        // the paper shows through. Seeds are constants — deterministic output — and the noise samples
        // user space, so every node lands on a different patch of the same field and no two cards
        // texture alike. One shared def serves all paths.
        if (style.Artwork == StyleArtwork.Sketch)
            defs += $"<filter id=\"beck-crayon-{h}\" x=\"-15%\" y=\"-15%\" width=\"130%\" height=\"130%\">" +
                    "<feTurbulence type=\"fractalNoise\" baseFrequency=\"0.036\" numOctaves=\"3\" seed=\"26\" result=\"warp\"/>" +
                    "<feDisplacementMap in=\"SourceGraphic\" in2=\"warp\" scale=\"6\" xChannelSelector=\"R\" yChannelSelector=\"G\" result=\"rough\"/>" +
                    // Anisotropic grain — long and low-frequency along x, tight along y — so the holes
                    // stretch into drag streaks that follow the colouring strokes, wax not spray-paint.
                    "<feTurbulence type=\"fractalNoise\" baseFrequency=\"0.28 1.2\" numOctaves=\"2\" seed=\"66\" result=\"grain\"/>" +
                    "<feColorMatrix in=\"grain\" type=\"matrix\" values=\"0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 -2.02 1.47\" result=\"mask\"/>" +
                    "<feComposite in=\"rough\" in2=\"mask\" operator=\"in\"/>" +
                    "</filter>";
        return defs;
    }
}
