using System.Globalization;
using System.Text;

namespace Beck;

/// <summary>
/// The <c>meta:</c> block shared by every diagram-type builder (title, theme,
/// animation, fit, spacing). Each builder holds one and delegates its fluent
/// meta setters here, so the YAML emission stays in a single place.
/// </summary>
internal sealed class MetaOptions
{
    public string? Title;
    public string? Subtitle;
    public Direction? Direction;
    public ThemeMode? Theme;
    public bool? Animate;
    public bool? Loop;
    public FitMode? Fit;
    public int? SpacingRank;
    public int? SpacingNode;
    public int? SpacingCornerRadius;

    public void AppendYaml(StringBuilder sb)
    {
        var hasSpacing = SpacingRank != null || SpacingNode != null || SpacingCornerRadius != null;
        var hasMeta = Title != null || Subtitle != null || Direction != null ||
                      Theme != null || Animate != null || Loop != null || Fit != null || hasSpacing;
        if (!hasMeta) return;

        sb.Append("meta:\n");
        if (Title != null) sb.Append("  title: ").Append(YamlWriter.Scalar(Title)).Append('\n');
        if (Subtitle != null) sb.Append("  subtitle: ").Append(YamlWriter.Scalar(Subtitle)).Append('\n');
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
    }
}
