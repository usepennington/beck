using System.Text;

namespace Beck.Rendering.Svg;

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
    /// <summary>Format a style integer (mix percentage, font weight) invariantly so a comma-decimal
    /// locale can never perturb the emitted CSS.</summary>
    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

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
        sb.Append($"{scope} .beck-node{{")
          .Append("fill:var(--beck-node-bg);")
          .Append($"stroke:color-mix(in srgb, var(--beck-accent) {P(mix.NodeStroke)}%, var(--beck-node-border));")
          .Append($"stroke-width:{Sw(geo.NodeStroke)};")
          .Append($"filter:{geo.NodeShadow};")
          .Append("}");
        sb.Append($"[data-theme='dark'] {scope} .beck-node,@media (prefers-color-scheme:dark){{:root:not([data-theme='light']) {scope} .beck-node}}{{filter:{geo.NodeShadowDark};}}");
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
        string glFamily = style.Typography.Roles.Of(Text.FontRole.GroupLabel).Mono ? "font-family:var(--beck-font-mono);" : "";
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
        sb.Append($"{scope} .beck-lifeline{{stroke-width:{Sw(geo.LifelineStroke)};stroke-dasharray:{strokes.LifelineDash};}}");
        sb.Append($"{scope} .beck-activation{{filter:drop-shadow(0 0 5px color-mix(in srgb, var(--beck-accent) {P(mix.ActivationGlow)}%, transparent));}}");
        sb.Append($"{scope} .beck-msg-chip{{fill:var(--beck-node-bg);stroke:color-mix(in srgb, var(--beck-accent) {P(mix.ChipStroke)}%, transparent);stroke-width:{Sw(geo.HairlineStroke)};}}");
        // Message labels honour the MsgText role's uppercase flag (blueprint's mono-uppercase
        // annotations, consistent with its edge/group/band labels). MsgText is a mono role, so
        // uppercasing is width-invariant — the measured chip still fits. Classic's MsgText is
        // non-uppercase → nothing appended → byte-identical.
        string mtCase = style.Typography.Roles.Of(Text.FontRole.MsgText).Uppercase ? "text-transform:uppercase;" : "";
        sb.Append($"{scope} .beck-msg-text{{fill:color-mix(in srgb, var(--beck-accent) {P(mix.MsgText)}%, var(--beck-text));{mtCase}}}");
        sb.Append($"{scope} .beck-msg--reply .beck-msg-chip{{stroke:none;}}");
        sb.Append($"{scope} .beck-msg--reply .beck-msg-text,{scope} .beck-msg-text--bare{{fill:var(--beck-text-muted);}}");
        sb.Append($"{scope} .beck-band-box{{fill:color-mix(in srgb, var(--beck-accent) {P(mix.BandFill)}%, transparent);stroke:color-mix(in srgb, var(--beck-accent) {P(mix.BandStroke)}%, transparent);stroke-width:{Sw(geo.BandBoxStroke)};stroke-dasharray:{strokes.GroupDash};}}");
        sb.Append($"{scope} .beck-band-chip{{fill:var(--beck-surface);stroke:color-mix(in srgb, var(--beck-accent) {P(mix.ChipStroke)}%, transparent);stroke-width:{Sw(geo.HairlineStroke)};}}");
        sb.Append($"{scope} .beck-band-label{{fill:color-mix(in srgb, var(--beck-accent) {P(mix.BandLabel)}%, var(--beck-text));}}");

        // edges + labels
        // DashedEdges (blueprint) dashes every base edge; classic leaves it solid (byte-identical).
        string edgeDash = strokes.DashedEdges ? $"stroke-dasharray:{strokes.EdgeDash};" : "";
        sb.Append($"{scope} .beck-edge{{fill:none;stroke-width:{Sw(geo.EdgeStroke)};stroke-linecap:round;stroke-linejoin:round;{edgeDash}}}");
        // Edge-label type honours the EdgeLabel role's family/case flags (mono uppercase for blueprint);
        // the textLength guard keeps the run inside its measured box, so this is layout-safe. Classic's
        // EdgeLabel is sans/non-uppercase → nothing appended → byte-identical.
        var elSpec = style.Typography.Roles.Of(Text.FontRole.EdgeLabel);
        string elType = (elSpec.Mono ? "font-family:var(--beck-font-mono);" : "") + (elSpec.Uppercase ? "text-transform:uppercase;" : "");
        sb.Append($"{scope} .beck-edge-label{{fill:var(--beck-text-muted);paint-order:stroke;stroke:var(--beck-surface);stroke-width:{geo.EdgeLabelHalo};stroke-linejoin:round;{elType}}}");

        // title block
        sb.Append($"{scope} .beck-title{{fill:var(--beck-text);}}");
        sb.Append($"{scope} .beck-subtitle{{fill:var(--beck-text-muted);}}");

        return sb.ToString();
    }

    /// <summary>
    /// Style-scoped SVG <c>&lt;defs&gt;</c> content (concatenated after the markers + animation defs).
    /// Today this is only glow's luminous edge gradient (<see cref="StyleStrokes.GradientEdges"/>): a
    /// single <c>&lt;linearGradient&gt;</c> whose id is scoped by the content hash and whose stops are
    /// <c>color-mix</c> expressions over <c>--beck-*</c> tokens — deterministic, theme-adaptive, and
    /// never a resolved literal. Endpoints hold the plain <c>--beck-edge</c> tone (matching the arrow
    /// markers, which stay edge-coloured) while the midpoint blooms toward accent/info. Returns
    /// <c>""</c> for every style that doesn't opt in (classic included), so the <c>&lt;defs&gt;</c>
    /// block is byte-identical.
    /// </summary>
    public static string StyleDefs(string h, BeckStyle style)
    {
        if (!style.Strokes.GradientEdges) return "";
        string id = $"beck-edge-grad-{h}";
        return $"<linearGradient id=\"{id}\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">" +
               "<stop offset=\"0\" stop-color=\"var(--beck-edge)\"/>" +
               "<stop offset=\"0.5\" stop-color=\"color-mix(in srgb, var(--beck-info) 50%, var(--beck-accent))\"/>" +
               "<stop offset=\"1\" stop-color=\"var(--beck-edge)\"/>" +
               "</linearGradient>";
    }
}
