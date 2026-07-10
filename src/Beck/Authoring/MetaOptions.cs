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
    public string? _title;
    public string? _subtitle;
    public string? _style;
    public Direction? _direction;
    public ThemeMode? _theme;
    public bool? _animate;
    public bool? _loop;
    public FitMode? _fit;
    public int? _spacingRank;
    public int? _spacingNode;
    public int? _spacingCornerRadius;
    public bool? _narrate;
    public int? _narrateWpm;
    public double? _narrateMin;
    public double? _narratePad;

    public void AppendYaml(StringBuilder sb)
    {
        var hasSpacing = _spacingRank != null || _spacingNode != null || _spacingCornerRadius != null;
        var hasNarrateKnobs = _narrateWpm != null || _narrateMin != null || _narratePad != null;
        var hasNarrate = _narrate != null || hasNarrateKnobs;
        var hasMeta = _title != null || _subtitle != null || _style != null || _direction != null ||
                      _theme != null || _animate != null || _loop != null || _fit != null ||
                      hasSpacing || hasNarrate;
        if (!hasMeta)
        {
            return;
        }

        sb.Append("meta:\n");
        if (_title != null)
        {
            sb.Append("  title: ").Append(YamlWriter.Scalar(_title)).Append('\n');
        }

        if (_subtitle != null)
        {
            sb.Append("  subtitle: ").Append(YamlWriter.Scalar(_subtitle)).Append('\n');
        }

        if (_style != null)
        {
            sb.Append("  style: ").Append(YamlWriter.Scalar(_style)).Append('\n');
        }

        if (_direction is { } d)
        {
            sb.Append("  direction: ").Append(Tokens.Of(d)).Append('\n');
        }

        if (_theme is { } t)
        {
            sb.Append("  theme: ").Append(Tokens.Of(t)).Append('\n');
        }

        if (_animate is { } a)
        {
            sb.Append("  animate: ").Append(a ? "true" : "false").Append('\n');
        }

        if (_loop is { } l)
        {
            sb.Append("  loop: ").Append(l ? "true" : "false").Append('\n');
        }

        if (_fit is { } f)
        {
            sb.Append("  fit: ").Append(Tokens.Of(f)).Append('\n');
        }

        if (hasSpacing)
        {
            sb.Append("  spacing:\n");
            if (_spacingRank is { } sr)
            {
                sb.Append("    rank: ").Append(sr.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            if (_spacingNode is { } sn)
            {
                sb.Append("    node: ").Append(sn.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            if (_spacingCornerRadius is { } sc)
            {
                sb.Append("    cornerRadius: ").Append(sc.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }
        if (hasNarrate)
        {
            // Bare toggle when no pacing knobs are set; otherwise a mapping (with
            // `enabled` only when explicitly turned off, matching the parser default).
            if (!hasNarrateKnobs)
            {
                sb.Append("  narrate: ").Append(_narrate!.Value ? "true" : "false").Append('\n');
            }
            else
            {
                sb.Append("  narrate:\n");
                if (_narrate is false)
                {
                    sb.Append("    enabled: false\n");
                }

                if (_narrateWpm is { } w)
                {
                    sb.Append("    wpm: ").Append(w.ToString(CultureInfo.InvariantCulture)).Append('\n');
                }

                if (_narrateMin is { } mn)
                {
                    sb.Append("    min: ").Append(mn.ToString(CultureInfo.InvariantCulture)).Append('\n');
                }

                if (_narratePad is { } pd)
                {
                    sb.Append("    pad: ").Append(pd.ToString(CultureInfo.InvariantCulture)).Append('\n');
                }
            }
        }
    }
}