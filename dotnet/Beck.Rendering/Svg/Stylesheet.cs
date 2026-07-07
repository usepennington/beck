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
    // Light token table — verbatim from styles.css:16-49.
    private static readonly (string Name, string Value)[] LightTokens =
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
        ("--beck-group-border", "color-mix(in srgb, var(--beck-neutral) 45%, transparent)"),
        ("--beck-group-label", "var(--beck-text-muted)"),
        ("--beck-edge", "var(--color-base-300, #cbd5e1)"),
        ("--beck-packet", "var(--beck-primary)"),
        ("--beck-icon-bg", "var(--color-base-100, #f1f5f9)"),
        ("--beck-accent", "var(--beck-primary)"),
    };

    // The nine dark overrides — styles.css:54-62.
    private static readonly (string Name, string Value)[] DarkTokens =
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
    };

    public static string Emit(string h, string fontFamily, string monoFamily, ThemeMode theme)
    {
        var sb = new StringBuilder();
        string scope = $".b-{h}";

        // ---- stratum 1: tokens ----
        void Block(string selector, (string, string)[] tokens)
        {
            sb.Append(selector).Append('{');
            foreach (var (name, value) in tokens) sb.Append(name).Append(':').Append(value).Append(';');
            sb.Append("--beck-font:").Append(fontFamily).Append(';');
            sb.Append("--beck-font-mono:").Append(monoFamily).Append(';');
            sb.Append('}');
        }

        switch (theme)
        {
            case ThemeMode.Light:
                Block(scope, LightTokens);
                break;
            case ThemeMode.Dark:
                Block(scope, LightTokens);
                Block(scope, DarkTokens);
                break;
            default: // auto — both host-controlled and standalone hooks
                Block(scope, LightTokens);
                Block($"[data-theme='dark'] {scope}", DarkTokens);
                sb.Append("@media (prefers-color-scheme: dark){");
                Block($":root:not([data-theme='light']) {scope}", DarkTokens);
                sb.Append('}');
                break;
        }

        // ---- stratum 2: shape CSS (SVG translation of styles.css) ----
        sb.Append(scope).Append("{font-family:var(--beck-font);}");

        // node card
        sb.Append($"{scope} .beck-node{{")
          .Append("fill:var(--beck-node-bg);")
          .Append("stroke:color-mix(in srgb, var(--beck-accent) 32%, var(--beck-node-border));")
          .Append("stroke-width:1.5;")
          .Append("filter:drop-shadow(0 1px 3px rgb(0 0 0/.05)) drop-shadow(0 4px 12px rgb(0 0 0/.06));")
          .Append("}");
        sb.Append($"[data-theme='dark'] {scope} .beck-node,@media (prefers-color-scheme:dark){{:root:not([data-theme='light']) {scope} .beck-node}}{{filter:drop-shadow(0 1px 3px rgb(0 0 0/.3)) drop-shadow(0 4px 14px rgb(0 0 0/.4));}}");
        sb.Append($"{scope} .beck-node--external{{stroke-dasharray:5 4;}}");
        sb.Append($"{scope} .beck-node--subtle{{opacity:.72;}}");
        sb.Append($"{scope} .beck-node--ghost{{fill:transparent;stroke-dasharray:5 4;filter:none;}}");

        // icon chip
        sb.Append($"{scope} .beck-icon-chip{{fill:color-mix(in srgb, var(--beck-accent) 15%, var(--beck-icon-bg));}}");
        sb.Append($"{scope} .beck-node--ghost .beck-icon-chip{{fill:transparent;}}");
        sb.Append($"{scope} .beck-icon{{color:var(--beck-accent);}}");

        // text
        sb.Append($"{scope} .beck-node-title{{fill:var(--beck-text);}}");
        sb.Append($"{scope} .beck-node-subtitle,{scope} .beck-ghost-label{{fill:var(--beck-text-muted);}}");
        sb.Append($"{scope} .beck-status-inline{{fill:var(--beck-accent);}}");

        // status pill
        sb.Append($"{scope} .beck-status-bg{{fill:color-mix(in srgb, var(--beck-accent) 14%, transparent);}}");
        sb.Append($"{scope} .beck-status-text{{fill:var(--beck-accent);}}");

        // group
        sb.Append($"{scope} .beck-group{{fill:none;stroke:var(--beck-group-border);stroke-width:1.5;stroke-dasharray:6 6;}}");
        sb.Append($"{scope} .beck-group-label-bg{{fill:var(--beck-surface);}}");
        sb.Append($"{scope} .beck-group-label{{fill:var(--beck-group-label);}}");

        // state pills reuse the card treatment; start/end pseudo-states
        sb.Append($"{scope} .beck-node--start{{fill:var(--beck-text-muted);}}");
        sb.Append($"{scope} .beck-node--end{{fill:none;stroke:var(--beck-text-muted);stroke-width:2;}}");
        sb.Append($"{scope} .beck-end-dot{{fill:var(--beck-text-muted);}}");

        // class compartment card
        sb.Append($"{scope} .beck-class-head{{fill:color-mix(in srgb, var(--beck-accent) 10%, transparent);}}");
        sb.Append($"{scope} .beck-class-head-border{{stroke:color-mix(in srgb, var(--beck-accent) 28%, var(--beck-node-border));stroke-width:1;}}");
        sb.Append($"{scope} .beck-class-divider{{stroke:var(--beck-node-border);stroke-width:1;}}");
        sb.Append($"{scope} .beck-class-stereo{{fill:var(--beck-text-muted);}}");
        sb.Append($"{scope} .beck-class-title{{fill:var(--beck-text);}}");
        sb.Append($"{scope} .beck-class-field{{fill:var(--beck-text-muted);}}");
        sb.Append($"{scope} .beck-class-method{{fill:var(--beck-text);}}");

        // edges + labels
        sb.Append($"{scope} .beck-edge{{fill:none;stroke-width:1.6;stroke-linecap:round;stroke-linejoin:round;}}");
        sb.Append($"{scope} .beck-edge-label{{fill:var(--beck-text-muted);paint-order:stroke;stroke:var(--beck-surface);stroke-width:3px;stroke-linejoin:round;}}");

        // title block
        sb.Append($"{scope} .beck-title{{fill:var(--beck-text);}}");
        sb.Append($"{scope} .beck-subtitle{{fill:var(--beck-text-muted);}}");

        return sb.ToString();
    }
}
