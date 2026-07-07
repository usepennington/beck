using System.Globalization;
using System.Text;
using Beck.Rendering.Svg;

namespace Beck.Rendering.Animate;

/// <summary>A node's card box in canvas coordinates (for overlay placement).</summary>
internal readonly record struct NodeBox(double X, double Y, double W, double H, double Rx);

/// <summary>Sequence storytelling context: the activation bars (by their start/end edge)
/// and the section-band count, used to dim + reveal the scenery as the story plays.</summary>
internal sealed record SeqChoreo(IReadOnlyList<(string Start, string End)> Bars, int BandCount);

/// <summary>
/// Compiles a <see cref="Schedule"/> into CSS keyframes on the shared-cycle model
/// (§10): every element animates over the whole cycle <c>T = Duration +
/// RepeatDelay</c>, its action a percentage window, all in lockstep and looping.
/// Packets ride <c>offset-path</c>; trails reveal via <c>stroke-dashoffset</c>;
/// node effects (pulse/highlight/fail incl. pulse-on-arrival) bounce a per-node
/// <c>.beck-fx-node</c> wrapper and flash ripple/glow overlays; the <c>impact</c>
/// knob expands a ring at each landing.
/// </summary>
internal sealed class CssCompiler
{
    private readonly string _h;
    private readonly Schedule _s;
    private readonly IReadOnlyList<NodeBox> _boxes;
    private readonly SeqChoreo? _choreo;
    private readonly bool _scrub;
    private readonly double _t;
    private readonly string _iter;
    private readonly string _cyc;
    private bool _needGlow;

    public CssCompiler(Schedule schedule, string hash, IReadOnlyList<NodeBox> boxes, SeqChoreo? choreo = null, bool scrub = false)
    {
        _s = schedule;
        _h = hash;
        _boxes = boxes;
        _choreo = choreo;
        _scrub = scrub;
        _t = schedule.Duration + schedule.RepeatDelay;
        _iter = schedule.Repeat == -1 ? "infinite" : schedule.Repeat == 0 ? "1" : (schedule.Repeat + 1).ToString(CultureInfo.InvariantCulture);
        // Scrub mode drives every keyframe track off scroll position instead of time:
        // `auto` duration + a view() timeline (added by ScrubTimeline). Same keyframes.
        _cyc = scrub ? "auto linear both" : $"{Nm(_t)}s linear {_iter}";
    }

    private double Pct(double time) => _t <= 0 ? 0 : Math.Clamp(time / _t * 100, 0, 100);
    private static string P(double pct) => Math.Round(pct, 4).ToString("0.####", CultureInfo.InvariantCulture);
    private static string Nm(double n) => SvgWriter.Num(n);
    private bool HasContent => _s.Packets.Count > 0 || _s.Cards.Count > 0 || _s.Impacts.Count > 0
        || _s.Edges.Count > 0 || _s.Working.Count > 0 || _s.Narrations.Count > 0 || _s.Statuses.Count > 0 || _choreo != null;

    private NodeBox? Box(int i) => i >= 0 && i < _boxes.Count ? _boxes[i] : null;

    /// <summary>The fx-layer markup (overlays behind, then trails + packet circles).</summary>
    public string Markup()
    {
        if (!HasContent) return "";
        var sb = new StringBuilder("<g class=\"beck-fx\">");

        // glow / ripple overlays (behind the packets) — one element per card effect.
        for (int j = 0; j < _s.Cards.Count; j++)
        {
            CardFx c = _s.Cards[j];
            if (Box(c.Node) is not { } b) continue;
            string col = SvgWriter.Attr(c.Color);
            string box = $"x=\"{Nm(b.X)}\" y=\"{Nm(b.Y)}\" width=\"{Nm(b.W)}\" height=\"{Nm(b.H)}\" rx=\"{Nm(b.Rx)}\" fill=\"none\" stroke=\"{col}\"";
            if (c.Kind == CardFxKind.Pulse)
                sb.Append($"<rect class=\"brip{j}-{_h}\" {box} stroke-width=\"2\" opacity=\"0\" style=\"transform-box:fill-box;transform-origin:center\"/>");
            else // highlight / fail — a glowing border overlay
                sb.Append($"<rect class=\"bgl{j}-{_h}\" {box} stroke-width=\"2\" opacity=\"0\" style=\"filter:drop-shadow(0 0 6px {c.Color})\"/>");
        }

        // impact rings (the `impact` knob) — expanding ring at each landing point.
        for (int j = 0; j < _s.Impacts.Count; j++)
        {
            ImpactFx im = _s.Impacts[j];
            sb.Append($"<circle class=\"bimp{j}-{_h}\" cx=\"{Nm(im.X)}\" cy=\"{Nm(im.Y)}\" r=\"{Nm(im.Radius)}\" fill=\"none\" stroke=\"{SvgWriter.Attr(im.Color)}\" stroke-width=\"2.5\" opacity=\"0\" style=\"transform-box:fill-box;transform-origin:center\"/>");
        }

        // edge overlays: activate (solid recolor) + stream (marching dashes).
        for (int j = 0; j < _s.Edges.Count; j++)
        {
            EdgeFx ef = _s.Edges[j];
            string col = SvgWriter.Attr(ef.Color);
            if (ef.Kind == EdgeFxKind.Activate)
                sb.Append($"<path class=\"bact{j}-{_h}\" d=\"{ef.D}\" fill=\"none\" stroke=\"{col}\" stroke-width=\"2\" opacity=\"0\"/>");
            else
                sb.Append($"<path class=\"bstr{j}-{_h}\" d=\"{ef.D}\" fill=\"none\" stroke=\"{col}\" stroke-width=\"2.5\" stroke-dasharray=\"5 9\" opacity=\"0\"/>");
        }

        // working breathing rings (card bounds; the pulse expands via stroke-width).
        for (int j = 0; j < _s.Working.Count; j++)
        {
            WorkFx wf = _s.Working[j];
            if (Box(wf.Node) is not { } b) continue;
            sb.Append($"<rect class=\"bwrk{j}-{_h}\" x=\"{Nm(b.X)}\" y=\"{Nm(b.Y)}\" width=\"{Nm(b.W)}\" height=\"{Nm(b.H)}\" rx=\"{Nm(b.Rx)}\" fill=\"none\" stroke=\"{SvgWriter.Attr(wf.Color)}\" stroke-width=\"0\" opacity=\"0\" style=\"transform-box:fill-box;transform-origin:center\"/>");
        }

        // trails (behind the dots)
        for (int i = 0; i < _s.Packets.Count; i++)
        {
            PacketHop p = _s.Packets[i];
            double off = p.Reversed ? -p.Length : p.Length;
            sb.Append($"<path class=\"beck-trail bt{i}-{_h}\" d=\"{p.D}\" fill=\"none\" stroke=\"{SvgWriter.Attr(p.Color)}\" stroke-width=\"2\" ")
              .Append($"style=\"stroke-dasharray:{Nm(p.Length)};stroke-dashoffset:{Nm(off)}\"/>");
        }
        // packet dots
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
            if (!string.IsNullOrEmpty(p.Label))
                sb.Append($"<text class=\"beck-packet-label bpl{i}-{_h}\" text-anchor=\"middle\" fill=\"{SvgWriter.Attr(p.Color)}\" opacity=\"0\" ")
                  .Append($"style=\"offset-path:path('{p.D}');offset-rotate:0deg;offset-anchor:center;transform:translateY(-{Nm(p.Size + 6)}px)\">{SvgWriter.Text(p.Label!)}</text>");
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
        if (!HasContent) return "";
        var sb = new StringBuilder();
        PacketCss(sb);
        CardCss(sb);
        EdgeCss(sb);
        WorkingCss(sb);
        NarrateCss(sb);
        SequenceChoreoCss(sb);
        StatusCss(sb);
        if (_scrub) ScrubTimeline(sb);
        return sb.ToString();
    }

    // Scrub mode: point every animated element at the view() scroll timeline, so
    // scrolling the diagram through the viewport scrubs the whole choreography.
    // Browsers without scroll-timelines ignore this and show the (both-filled) frame.
    private void ScrubTimeline(StringBuilder sb) =>
        sb.Append($".b-{_h} .beck-fx>*,.b-{_h} .beck-fx-node,.b-{_h} .beck-msg path,")
          .Append($".b-{_h} .beck-msg-chip,.b-{_h} .beck-msg-text,.b-{_h} .beck-band,")
          .Append($".b-{_h} .beck-activation,.b-{_h} .beck-status-state,.b-{_h} .beck-beat")
          .Append("{animation-timeline:view(block 90% 10%);}");

    // ---- status pills: one visible state at a time, instant swaps, restore to state 0 ----
    private void StatusCss(StringBuilder sb)
    {
        foreach (var byNode in _s.Statuses.GroupBy(s => s.Node))
        {
            int node = byNode.Key;
            var sw = new List<(double At, int State)> { (0, 0) };
            sw.AddRange(byNode.OrderBy(s => s.At).Select(s => (s.At, s.State)));
            sw.Add((_s.RestoreAt, 0));
            sw.Sort((a, b) => a.At.CompareTo(b.At));

            foreach (int st in sw.Select(x => x.State).Distinct())
            {
                string kf = $"kst{node}-{st}-{_h}";
                sb.Append($".b-{_h} .bss{node}-{st}-{_h}{{animation:{kf} {_cyc};}}");
                sb.Append($"@keyframes {kf}{{");
                double e = 0.01;
                int prev = 0;
                for (int k = 0; k < sw.Count - 1; k++)
                {
                    int val = sw[k].State == st ? 1 : 0;
                    double at = Pct(sw[k].At);
                    if (k == 0) { sb.Append($"0%{{opacity:{val};}}"); prev = val; continue; }
                    if (val != prev)
                    {
                        if (at > e) sb.Append($"{P(at - e)}%{{opacity:{prev};}}");
                        sb.Append($"{P(at)}%{{opacity:{val};}}");
                        prev = val;
                    }
                }
                sb.Append($"100%{{opacity:{prev};}}}}");
            }
        }
    }

    // ---- sequence storytelling: dim the scenery, reveal each row as its packet fires ----
    private const double DimLine = 0.15, DimLabel = 0.35, DimAct = 0.25, DimBand = 0.45;

    private void SequenceChoreoCss(StringBuilder sb)
    {
        if (_choreo is null) return;
        double finaleAt = Math.Max(0, _s.Duration - 0.75);

        // initial dims (the whole block is motion-guarded by the caller).
        sb.Append($".b-{_h} .beck-msg path{{opacity:{G(DimLine)};}}");
        sb.Append($".b-{_h} .beck-msg-chip,.b-{_h} .beck-msg-text{{opacity:{G(DimLabel)};}}");
        sb.Append($".b-{_h} .beck-band{{opacity:{G(DimBand)};}}");
        sb.Append($".b-{_h} .beck-activation{{opacity:{G(DimAct)};}}");

        // first departure + arrival per message edge.
        var revealAt = new Dictionary<string, (double At, double Arr)>();
        foreach (PacketHop p in _s.Packets)
            if (p.EdgeId is { } id && !revealAt.ContainsKey(id)) revealAt[id] = (p.Start, p.Start + p.Duration);

        int idx = 0;
        foreach (var (id, t) in revealAt)
        {
            string esc = id.Replace("\"", "\\\"");
            RevealTrack(sb, $"kchl{idx}-{_h}", $".b-{_h} .beck-msg[data-msg=\"{esc}\"] path", DimLine, t.At, 0.25, finaleAt);
            RevealTrack(sb, $"kcht{idx}-{_h}",
                $".b-{_h} .beck-msg[data-msg=\"{esc}\"] .beck-msg-chip,.b-{_h} .beck-msg[data-msg=\"{esc}\"] .beck-msg-text",
                DimLabel, t.At, 0.25, finaleAt);
            idx++;
        }

        // activation bars: brighten at their start edge, fade after their end edge.
        for (int i = 0; i < _choreo.Bars.Count; i++)
        {
            var (start, end) = _choreo.Bars[i];
            double? sT = revealAt.TryGetValue(start, out var s0) ? Math.Max(s0.At, s0.Arr - 0.15) : null;
            double? eT = revealAt.TryGetValue(end, out var e0) ? e0.Arr : null;
            BarTrack(sb, i, sT, eT, finaleAt);
        }

        // section bands: light up in phase order.
        for (int i = 0; i < _choreo.BandCount && i < _s.Phases.Count; i++)
            RevealTrack(sb, $"kchb{i}-{_h}", $".b-{_h} .beck-band[data-band=\"{i}\"]", DimBand, _s.Phases[i], 0.4, finaleAt);
    }

    // dim -> (reveal to 1 over revealDur at revealAt) -> hold -> (finale back to dim over 0.6s)
    private void RevealTrack(StringBuilder sb, string kf, string selector, double dim, double revealAt, double revealDur, double finaleAt)
    {
        double rs = Pct(revealAt), re = Pct(revealAt + revealDur), fs = Pct(finaleAt), fe = Pct(finaleAt + 0.6), e = 0.01;
        sb.Append($"{selector}{{animation:{kf} {_cyc};}}");
        sb.Append($"@keyframes {kf}{{0%{{opacity:{G(dim)};}}");
        if (rs > e) sb.Append($"{P(rs - e)}%{{opacity:{G(dim)};}}");
        sb.Append($"{P(rs)}%{{opacity:{G(dim)};}}");
        sb.Append($"{P(re)}%{{opacity:1;}}");
        if (fs > re) sb.Append($"{P(fs)}%{{opacity:1;}}");
        if (fe > fs) sb.Append($"{P(fe)}%{{opacity:{G(dim)};}}");
        sb.Append($"100%{{opacity:{G(dim)};}}}}");
    }

    private void BarTrack(StringBuilder sb, int i, double? startSec, double? endSec, double finaleSec)
    {
        string kf = $"kcha{i}-{_h}";
        double e = 0.01;
        sb.Append($".b-{_h} .beck-activation[data-bar=\"{i}\"]{{animation:{kf} {_cyc};}}");
        sb.Append($"@keyframes {kf}{{0%{{opacity:{G(DimAct)};}}");
        if (startSec is { } ss)
        {
            double s = Pct(ss), se = Pct(ss + 0.3);
            if (s > e) sb.Append($"{P(s - e)}%{{opacity:{G(DimAct)};}}");
            sb.Append($"{P(s)}%{{opacity:{G(DimAct)};}}{P(se)}%{{opacity:1;}}");
        }
        double fadeAt = endSec ?? finaleSec;
        double fs = Pct(fadeAt), fe = Pct(fadeAt + 0.35);
        sb.Append($"{P(fs)}%{{opacity:1;}}{P(fe)}%{{opacity:{G(DimAct)};}}");
        sb.Append($"100%{{opacity:{G(DimAct)};}}}}");
    }

    private static string G(double n) => n.ToString("0.##", CultureInfo.InvariantCulture);

    // ---- narration beats: sequential cross-fades (out 0.12 power1.in, in 0.3 power2.out) ----
    private void NarrateCss(StringBuilder sb)
    {
        var beats = _s.Narrations;
        for (int i = 0; i < beats.Count; i++)
        {
            double inS = Pct(beats[i].At + 0.12), inE = Pct(beats[i].At + 0.42), e = 0.01;
            // Fade out when the next beat begins (else hold to the restore point).
            double outS = i + 1 < beats.Count ? Pct(beats[i + 1].At) : Pct(_s.RestoreAt);
            double outE = i + 1 < beats.Count ? Pct(beats[i + 1].At + 0.12) : Math.Min(100, Pct(_s.RestoreAt) + 0.5);
            string pin = Easing.ToCss(Easing.Power1In), pout = Easing.ToCss(Easing.Power2Out);

            sb.Append($".b-{_h} .bbeat{i}-{_h}{{animation:kbe{i}-{_h} {_cyc};}}");
            sb.Append($"@keyframes kbe{i}-{_h}{{0%{{opacity:0;}}");
            if (inS > e) sb.Append($"{P(inS - e)}%{{opacity:0;}}");
            sb.Append($"{P(inS)}%{{opacity:0;animation-timing-function:{pout};}}");
            sb.Append($"{P(inE)}%{{opacity:1;}}");
            if (outS > inE) sb.Append($"{P(outS)}%{{opacity:1;animation-timing-function:{pin};}}");
            if (outE > outS) sb.Append($"{P(outE)}%{{opacity:0;}}");
            sb.Append("100%{opacity:0;}}");
        }
    }

    // ---- edge overlays: activate (instant recolor) + stream (marching dashes) ----
    private void EdgeCss(StringBuilder sb)
    {
        double restore = Pct(_s.RestoreAt);
        bool anyStream = false;
        for (int j = 0; j < _s.Edges.Count; j++)
        {
            EdgeFx ef = _s.Edges[j];
            string cls = ef.Kind == EdgeFxKind.Activate ? $"bact{j}-{_h}" : $"bstr{j}-{_h}";
            string kf = ef.Kind == EdgeFxKind.Activate ? $"kact{j}-{_h}" : $"kstr{j}-{_h}";
            string extra = "";
            if (ef.Kind == EdgeFxKind.Stream)
            {
                anyStream = true;
                double march = Math.Max(0.5, ef.Length / 220);
                extra = $", bmarch-{_h} {Nm(march)}s linear infinite";
            }
            sb.Append($".b-{_h} .{cls}{{animation:{kf} {_cyc}{extra};}}");
            GateKeyframes(sb, kf, Pct(ef.Start), restore);
        }
        if (anyStream) sb.Append($"@keyframes bmarch-{_h}{{to{{stroke-dashoffset:-14;}}}}");
    }

    // ---- working: a gated opacity window + an infinite breathing ring ----
    private void WorkingCss(StringBuilder sb)
    {
        if (_s.Working.Count == 0) return;
        for (int j = 0; j < _s.Working.Count; j++)
        {
            WorkFx wf = _s.Working[j];
            if (Box(wf.Node) is null) continue;
            string kf = $"kwrk{j}-{_h}";
            sb.Append($".b-{_h} .bwrk{j}-{_h}{{animation:{kf} {_cyc}, bbreath-{_h} 1.5s ease-in-out infinite;}}");
            double s = Pct(wf.Start), end = Pct(wf.End), e = 0.01;
            sb.Append($"@keyframes {kf}{{0%{{opacity:0;}}");
            if (s > e) sb.Append($"{P(s - e)}%{{opacity:0;}}");
            sb.Append($"{P(s)}%{{opacity:1;}}");
            if (end > s) sb.Append($"{P(end)}%{{opacity:1;}}");
            if (end + e < 100) sb.Append($"{P(end + e)}%{{opacity:0;}}");
            sb.Append("100%{opacity:0;}}");
        }
        // breathing: expand + fade the ring (stroke-based rebuild of the box-shadow pulse).
        sb.Append($"@keyframes bbreath-{_h}{{0%{{stroke-width:0;stroke-opacity:0.55;}}100%{{stroke-width:18;stroke-opacity:0;}}}}");
    }

    /// <summary>Instant-on at <paramref name="start"/>, instant-off at <paramref name="restore"/> (steps-end pairs).</summary>
    private void GateKeyframes(StringBuilder sb, string kf, double start, double restore)
    {
        double e = 0.01;
        sb.Append($"@keyframes {kf}{{0%{{opacity:0;}}");
        if (start > e) sb.Append($"{P(start - e)}%{{opacity:0;}}");
        sb.Append($"{P(start)}%{{opacity:1;}}");
        if (restore > start) sb.Append($"{P(restore)}%{{opacity:1;}}");
        if (restore + e < 100) sb.Append($"{P(restore + e)}%{{opacity:0;}}");
        sb.Append("100%{opacity:0;}}");
    }

    // ---- packets + trails (M8) ----
    private void PacketCss(StringBuilder sb)
    {
        double restore = Pct(_s.RestoreAt);
        for (int i = 0; i < _s.Packets.Count; i++)
        {
            PacketHop p = _s.Packets[i];
            double ws = Pct(p.Start), we = Pct(p.Start + p.Duration);
            double e = 0.01;
            string ease = Easing.ToCss(p.Ease);
            string startDist = p.Reversed ? "100%" : "0%";
            string endDist = p.Reversed ? "0%" : "100%";

            void Rider(string cls)
            {
                sb.Append($".b-{_h} .{cls}{i}-{_h}{{animation:kp{i}-{_h} {_cyc};}}");
            }
            Rider("bp");
            sb.Append($"@keyframes kp{i}-{_h}{{");
            sb.Append($"0%{{offset-distance:{startDist};opacity:0;}}");
            if (ws > e) sb.Append($"{P(ws - e)}%{{opacity:0;}}");
            sb.Append($"{P(ws)}%{{offset-distance:{startDist};opacity:1;animation-timing-function:{ease};}}");
            sb.Append($"{P(we)}%{{offset-distance:{endDist};opacity:1;}}");
            if (we + e < 100) sb.Append($"{P(we + e)}%{{opacity:0;}}");
            sb.Append($"100%{{offset-distance:{startDist};opacity:0;}}}}");

            // label rides the same keyframes (offset-path shared; its own animation ref)
            if (!string.IsNullOrEmpty(p.Label))
                sb.Append($".b-{_h} .bpl{i}-{_h}{{animation:kp{i}-{_h} {_cyc};}}");

            // trail: reveal then hold, snap back at restore
            double off = p.Reversed ? -p.Length : p.Length;
            sb.Append($".b-{_h} .bt{i}-{_h}{{animation:kt{i}-{_h} {_cyc};}}");
            sb.Append($"@keyframes kt{i}-{_h}{{");
            sb.Append($"0%{{stroke-dashoffset:{Nm(off)};}}");
            sb.Append($"{P(ws)}%{{stroke-dashoffset:{Nm(off)};animation-timing-function:{ease};}}");
            sb.Append($"{P(we)}%{{stroke-dashoffset:0;}}");
            if (restore > we) sb.Append($"{P(restore)}%{{stroke-dashoffset:0;}}");
            if (restore + e < 100) sb.Append($"{P(restore + e)}%{{stroke-dashoffset:{Nm(off)};}}");
            sb.Append($"100%{{stroke-dashoffset:{Nm(off)};}}}}");
        }
    }

    // ---- node card effects: transform tracks + overlays (M9) ----
    private void CardCss(StringBuilder sb)
    {
        // per-node transform track (pulse/highlight bounce, fail shake) merged into one animation
        var byNode = new Dictionary<int, List<CardFx>>();
        foreach (CardFx c in _s.Cards)
        {
            if (!byNode.TryGetValue(c.Node, out var list)) byNode[c.Node] = list = new();
            list.Add(c);
        }
        foreach (var (node, list) in byNode) TransformTrack(sb, node, list);

        // ripple / glow overlays (one keyframe animation each — no target sharing)
        for (int j = 0; j < _s.Cards.Count; j++)
        {
            CardFx c = _s.Cards[j];
            if (Box(c.Node) is null) continue;
            if (c.Kind == CardFxKind.Pulse) RippleCss(sb, j, c.Start);
            else GlowCss(sb, j, c.Start, c.Kind == CardFxKind.Highlight ? 0.21 : 0.12, c.Kind == CardFxKind.Highlight ? 0.7 : 1.0);
        }

        // impact rings
        for (int j = 0; j < _s.Impacts.Count; j++) ImpactCss(sb, j, _s.Impacts[j].Start);
    }

    private void TransformTrack(StringBuilder sb, int node, List<CardFx> list)
    {
        var pts = new List<(double T, string Tf, Ease? E)> { (0, "none", null) };
        foreach (CardFx c in list.OrderBy(c => c.Start))
        {
            double s = c.Start;
            switch (c.Kind)
            {
                case CardFxKind.Pulse:
                    pts.Add((s, "none", Easing.BackOut(3)));
                    pts.Add((s + 0.18, "translateY(-2px) scale(1.04)", Easing.ElasticOut(1, 0.5)));
                    pts.Add((s + 0.6, "none", null));
                    break;
                case CardFxKind.Highlight:
                    pts.Add((s, "none", Easing.BackOut(2)));
                    pts.Add((s + 0.21, "translateY(-2px) scale(1.04)", Easing.ElasticOut(1, 0.4)));
                    pts.Add((s + 0.7, "none", null));
                    break;
                case CardFxKind.Fail:
                    pts.Add((s + 0.12, "none", null));
                    pts.Add((s + 0.18, "translateX(-5px)", null));
                    pts.Add((s + 0.26, "translateX(5px)", null));
                    pts.Add((s + 0.33, "translateX(-3px)", null));
                    pts.Add((s + 0.40, "none", null));
                    break;
            }
        }
        pts.Add((_t, "none", null));
        pts.Sort((a, b) => a.T.CompareTo(b.T));

        sb.Append($".b-{_h} .bn{node}-{_h} .beck-fx-node{{animation:kn{node}-{_h} {_cyc};}}");
        sb.Append($"@keyframes kn{node}-{_h}{{");
        string lastPct = "";
        foreach (var (t, tf, e) in pts)
        {
            string pct = P(Pct(t));
            if (pct == lastPct) continue; // collapse coincident keyframes (last effect wins)
            lastPct = pct;
            sb.Append($"{pct}%{{transform:{tf};");
            if (e is { } ee) sb.Append($"animation-timing-function:{Easing.ToCss(ee)};");
            sb.Append('}');
        }
        sb.Append('}');
    }

    private void RippleCss(StringBuilder sb, int j, double start)
    {
        double s = Pct(start), end = Pct(start + 0.48), e = 0.01;
        string po = Easing.ToCss(Easing.Power2Out);
        sb.Append($".b-{_h} .brip{j}-{_h}{{animation:krip{j}-{_h} {_cyc};}}");
        sb.Append($"@keyframes krip{j}-{_h}{{");
        sb.Append("0%{opacity:0;transform:scale(1);}");
        if (s > e) sb.Append($"{P(s - e)}%{{opacity:0;transform:scale(1);}}");
        sb.Append($"{P(s)}%{{opacity:0.6;transform:scale(1);animation-timing-function:{po};}}");
        sb.Append($"{P(end)}%{{opacity:0;transform:scale(1.15);}}");
        sb.Append("100%{opacity:0;transform:scale(1);}}");
    }

    private void GlowCss(StringBuilder sb, int j, double start, double fadeIn, double total)
    {
        double s = Pct(start), up = Pct(start + fadeIn), hold = Pct(start + total), e = 0.01;
        sb.Append($".b-{_h} .bgl{j}-{_h}{{animation:kgl{j}-{_h} {_cyc};}}");
        sb.Append($"@keyframes kgl{j}-{_h}{{");
        sb.Append("0%{opacity:0;}");
        if (s > e) sb.Append($"{P(s - e)}%{{opacity:0;}}");
        sb.Append($"{P(s)}%{{opacity:0;}}");
        sb.Append($"{P(up)}%{{opacity:1;}}");
        sb.Append($"{P(hold)}%{{opacity:1;}}");
        if (hold + e < 100) sb.Append($"{P(hold + e)}%{{opacity:0;}}");
        sb.Append("100%{opacity:0;}}");
    }

    private void ImpactCss(StringBuilder sb, int j, double start)
    {
        double s = Pct(start), end = Pct(start + 0.55), e = 0.01;
        string po = Easing.ToCss(Easing.Power2Out);
        sb.Append($".b-{_h} .bimp{j}-{_h}{{animation:kimp{j}-{_h} {_cyc};}}");
        sb.Append($"@keyframes kimp{j}-{_h}{{");
        sb.Append("0%{opacity:0;transform:scale(1);stroke-width:2.5;}");
        if (s > e) sb.Append($"{P(s - e)}%{{opacity:0;transform:scale(1);}}");
        sb.Append($"{P(s)}%{{opacity:0.9;transform:scale(1);stroke-width:2.5;animation-timing-function:{po};}}");
        sb.Append($"{P(end)}%{{opacity:0;transform:scale(3.4);stroke-width:0.5;}}");
        sb.Append("100%{opacity:0;transform:scale(1);stroke-width:2.5;}}");
    }
}
