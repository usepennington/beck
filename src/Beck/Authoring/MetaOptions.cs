using System.Globalization;
using System.Text;

namespace Beck.Authoring;

/// <summary>
/// The <c>meta:</c> block shared by every diagram-type builder (title, theme,
/// animation, fit, spacing). Each builder holds one and delegates its fluent
/// meta setters here, so the YAML emission stays in a single place.
/// </summary>
internal sealed class MetaOptions
{
    public string? Title;
    public string? Subtitle;
    public string? Style;
    public Direction? Direction;
    public ThemeMode? Theme;
    public bool? Animate;
    public bool? Loop;
    public FitMode? Fit;
    public int? SpacingRank;
    public int? SpacingNode;
    public int? SpacingCornerRadius;
    public bool? Narrate;
    public int? NarrateWpm;
    public double? NarrateMin;
    public double? NarratePad;

    public void AppendYaml(StringBuilder sb)
    {
        var hasSpacing = SpacingRank != null || SpacingNode != null || SpacingCornerRadius != null;
        var hasNarrateKnobs = NarrateWpm != null || NarrateMin != null || NarratePad != null;
        var hasNarrate = Narrate != null || hasNarrateKnobs;
        var hasMeta = Title != null || Subtitle != null || Style != null || Direction != null ||
                      Theme != null || Animate != null || Loop != null || Fit != null ||
                      hasSpacing || hasNarrate;
        if (!hasMeta) return;

        sb.Append("meta:\n");
        if (Title != null) sb.Append("  title: ").Append(YamlWriter.Scalar(Title)).Append('\n');
        if (Subtitle != null) sb.Append("  subtitle: ").Append(YamlWriter.Scalar(Subtitle)).Append('\n');
        if (Style != null) sb.Append("  style: ").Append(YamlWriter.Scalar(Style)).Append('\n');
        if (Direction is { } d) sb.Append("  direction: ").Append(Tokens.Of(d)).Append('\n');
        if (Theme is { } t) sb.Append("  theme: ").Append(Tokens.Of(t)).Append('\n');
        if (Animate is { } a) sb.Append("  animate: ").Append(a ? "true" : "false").Append('\n');
        if (Loop is { } l) sb.Append("  loop: ").Append(l ? "true" : "false").Append('\n');
        if (Fit is { } f) sb.Append("  fit: ").Append(Tokens.Of(f)).Append('\n');
        if (hasSpacing)
        {
            sb.Append("  spacing:\n");
            if (SpacingRank is { } sr) sb.Append("    rank: ").Append(sr.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (SpacingNode is { } sn) sb.Append("    node: ").Append(sn.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (SpacingCornerRadius is { } sc) sb.Append("    cornerRadius: ").Append(sc.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        if (hasNarrate)
        {
            // Bare toggle when no pacing knobs are set; otherwise a mapping (with
            // `enabled` only when explicitly turned off, matching the parser default).
            if (!hasNarrateKnobs)
            {
                sb.Append("  narrate: ").Append(Narrate!.Value ? "true" : "false").Append('\n');
            }
            else
            {
                sb.Append("  narrate:\n");
                if (Narrate is false) sb.Append("    enabled: false\n");
                if (NarrateWpm is { } w) sb.Append("    wpm: ").Append(w.ToString(CultureInfo.InvariantCulture)).Append('\n');
                if (NarrateMin is { } mn) sb.Append("    min: ").Append(mn.ToString(CultureInfo.InvariantCulture)).Append('\n');
                if (NarratePad is { } pd) sb.Append("    pad: ").Append(pd.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }
    }
}
