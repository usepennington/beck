using System.Globalization;
using System.Text;
using Beck.Rendering.Svg;

namespace Beck.Rendering.Animate;

/// <summary>
/// Compiles a <see cref="Schedule"/> into CSS keyframes on the shared-cycle model
/// (§10): every element animates over the whole cycle <c>T = Duration +
/// RepeatDelay</c>, its action a percentage window, all in lockstep and looping.
/// Packets ride <c>offset-path</c>; trails reveal via <c>stroke-dashoffset</c>.
/// The core motion (M8); the full effect vocabulary is M9.
/// </summary>
internal sealed class CssCompiler
{
    private readonly string _h;
    private readonly Schedule _s;
    private readonly double _t;
    private readonly string _iter;
    private bool _needGlow;

    public CssCompiler(Schedule schedule, string hash)
    {
        _s = schedule;
        _h = hash;
        _t = schedule.Duration + schedule.RepeatDelay;
        _iter = schedule.Repeat == -1 ? "infinite" : schedule.Repeat == 0 ? "1" : (schedule.Repeat + 1).ToString(CultureInfo.InvariantCulture);
    }

    private double Pct(double time) => _t <= 0 ? 0 : Math.Clamp(time / _t * 100, 0, 100);
    private static string P(double pct) => Math.Round(pct, 4).ToString("0.####", CultureInfo.InvariantCulture);
    private static string Nm(double n) => SvgWriter.Num(n);

    /// <summary>The fx-layer markup (packet circles + trail paths).</summary>
    public string Markup()
    {
        if (_s.Packets.Count == 0) return "";
        var sb = new StringBuilder("<g class=\"beck-fx\">");
        // trails first (behind the dots)
        for (int i = 0; i < _s.Packets.Count; i++)
        {
            PacketHop p = _s.Packets[i];
            double off = p.Reversed ? -p.Length : p.Length;
            sb.Append($"<path class=\"beck-trail bt{i}-{_h}\" d=\"{p.D}\" fill=\"none\" stroke=\"{SvgWriter.Attr(p.Color)}\" stroke-width=\"2\" ")
              .Append($"style=\"stroke-dasharray:{Nm(p.Length)};stroke-dashoffset:{Nm(off)}\"/>");
        }
        for (int i = 0; i < _s.Packets.Count; i++)
        {
            PacketHop p = _s.Packets[i];
            string fillStroke = p.Shape == PacketShape.Ring
                ? $"fill=\"none\" stroke=\"{SvgWriter.Attr(p.Color)}\" stroke-width=\"{Nm(Math.Max(2.5, p.Size * 0.28))}\""
                : $"fill=\"{SvgWriter.Attr(p.Color)}\"";
            string glow = p.Glow ? $" filter=\"url(#beck-glow-{_h})\"" : "";
            if (p.Glow) _needGlow = true;
            sb.Append($"<circle class=\"beck-packet bp{i}-{_h}\" r=\"{Nm(p.Size)}\" {fillStroke}{glow} opacity=\"0\" ")
              .Append($"style=\"offset-path:path('{p.D}');offset-rotate:0deg\"/>");
        }
        return sb.Append("</g>").ToString();
    }

    /// <summary>The glow filter def (only if a packet needs it — call after Markup()).</summary>
    public string Defs() => _needGlow
        ? $"<filter id=\"beck-glow-{_h}\" x=\"-200%\" y=\"-200%\" width=\"500%\" height=\"500%\">"
          + "<feGaussianBlur in=\"SourceGraphic\" stdDeviation=\"3\" result=\"blur\"/>"
          + "<feMerge><feMergeNode in=\"blur\"/><feMergeNode in=\"SourceGraphic\"/></feMerge></filter>"
        : "";

    /// <summary>The animation CSS (wrapped in a reduced-motion guard by the caller).</summary>
    public string Css()
    {
        if (_s.Packets.Count == 0) return "";
        var sb = new StringBuilder();
        double restore = Pct(_s.RestoreAt);
        for (int i = 0; i < _s.Packets.Count; i++)
        {
            PacketHop p = _s.Packets[i];
            double ws = Pct(p.Start), we = Pct(p.Start + p.Duration);
            double e = 0.01;
            string ease = Easing.ToCss(p.Ease);
            string startDist = p.Reversed ? "100%" : "0%";
            string endDist = p.Reversed ? "0%" : "100%";

            // packet: opacity + offset-distance
            sb.Append($".b-{_h} .bp{i}-{_h}{{animation:kp{i}-{_h} {Nm(_t)}s linear {_iter};}}");
            sb.Append($"@keyframes kp{i}-{_h}{{");
            sb.Append($"0%{{offset-distance:{startDist};opacity:0;}}");
            if (ws > e) sb.Append($"{P(ws - e)}%{{opacity:0;}}");
            sb.Append($"{P(ws)}%{{offset-distance:{startDist};opacity:1;animation-timing-function:{ease};}}");
            sb.Append($"{P(we)}%{{offset-distance:{endDist};opacity:1;}}");
            if (we + e < 100) sb.Append($"{P(we + e)}%{{opacity:0;}}");
            sb.Append($"100%{{offset-distance:{startDist};opacity:0;}}}}");

            // trail: reveal then hold, snap back at restore
            double off = p.Reversed ? -p.Length : p.Length;
            sb.Append($".b-{_h} .bt{i}-{_h}{{animation:kt{i}-{_h} {Nm(_t)}s linear {_iter};}}");
            sb.Append($"@keyframes kt{i}-{_h}{{");
            sb.Append($"0%{{stroke-dashoffset:{Nm(off)};}}");
            sb.Append($"{P(ws)}%{{stroke-dashoffset:{Nm(off)};animation-timing-function:{ease};}}");
            sb.Append($"{P(we)}%{{stroke-dashoffset:0;}}");
            if (restore > we) sb.Append($"{P(restore)}%{{stroke-dashoffset:0;}}");
            if (restore + e < 100) sb.Append($"{P(restore + e)}%{{stroke-dashoffset:{Nm(off)};}}");
            sb.Append($"100%{{stroke-dashoffset:{Nm(off)};}}}}");
        }
        return sb.ToString();
    }
}
