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
/// One per-edge overlay layer (<see cref="EdgeOverlay"/>): the class-indexed decoration path sharing
/// the edge's exact <c>d</c>, its measured length (for the comet/draw-on dash window), and a baked
/// phase fraction in [0,1) (the per-edge comet offset). The markup (the <c>&lt;path&gt;</c> with its
/// static dasharray) is emitted by the edge/message painters; <see cref="CssCompiler.EdgeOverlayCss"/>
/// compiles the motion.
/// </summary>
internal sealed record EdgeOverlaySpec(int Index, EdgeOverlay Mode, double Length, double Phase);

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
    // Card-effect eases are parameter-fixed and deterministic, so their sampled CSS is hoisted
    // once here instead of re-running Easing.ToCss per element per render.
    private static readonly string PulseInCss = Easing.ToCss(Easing.BackOut(3));
    private static readonly string PulsePeakCss = Easing.ToCss(Easing.ElasticOut(1, 0.5));
    private static readonly string HighlightInCss = Easing.ToCss(Easing.BackOut(2));
    private static readonly string HighlightPeakCss = Easing.ToCss(Easing.ElasticOut(1, 0.4));
    private static readonly string Power2OutCss = Easing.ToCss(Easing.Power2Out);

    private readonly string _h;
    private readonly Schedule _s;
    private readonly IReadOnlyList<NodeBox> _boxes;
    private readonly StyleMotion _motion;
    private readonly StyleStrokes _strokes;
    private readonly SeqChoreo? _choreo;
    private readonly bool _scrub;
    private readonly double _t;
    private readonly string _iter;
    private readonly string _cyc;
    private bool _needGlow;

    public CssCompiler(Schedule schedule, string hash, IReadOnlyList<NodeBox> boxes, StyleMotion motion, StyleStrokes strokes, SeqChoreo? choreo = null, bool scrub = false)
    {
        _s = schedule;
        _h = hash;
        _boxes = boxes;
        _motion = motion;
        _strokes = strokes;
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
                sb.Append($"<rect class=\"brip{j}-{_h}\" {box} stroke-width=\"{Nm(_motion.OverlayStroke)}\" opacity=\"0\" style=\"transform-box:fill-box;transform-origin:center\"/>");
            else // highlight / fail — a glowing border overlay
                sb.Append($"<rect class=\"bgl{j}-{_h}\" {box} stroke-width=\"{Nm(_motion.OverlayStroke)}\" opacity=\"0\" style=\"filter:drop-shadow(0 0 {Nm(6 * _motion.EffectAmplitude)}px {c.Color})\"/>");
        }

        // impact rings (the `impact` knob) — expanding ring at each landing point. Gated off
        // wholesale by a style with RingsEnabled=false (minimal's "rings off" identity).
        if (_motion.RingsEnabled)
            for (int j = 0; j < _s.Impacts.Count; j++)
            {
                ImpactFx im = _s.Impacts[j];
                sb.Append($"<circle class=\"bimp{j}-{_h}\" cx=\"{Nm(im.X)}\" cy=\"{Nm(im.Y)}\" r=\"{Nm(im.Radius)}\" fill=\"none\" stroke=\"{SvgWriter.Attr(im.Color)}\" stroke-width=\"{Nm(_motion.RingStroke)}\" opacity=\"0\" style=\"transform-box:fill-box;transform-origin:center\"/>");
            }

        // edge overlays: activate (solid recolor) + stream (marching dashes).
        for (int j = 0; j < _s.Edges.Count; j++)
        {
            EdgeFx ef = _s.Edges[j];
            string col = SvgWriter.Attr(ef.Color);
            if (ef.Kind == EdgeFxKind.Activate)
                sb.Append($"<path class=\"bact{j}-{_h}\" d=\"{ef.D}\" fill=\"none\" stroke=\"{col}\" stroke-width=\"{Nm(_motion.OverlayStroke)}\" opacity=\"0\"/>");
            else
                sb.Append($"<path class=\"bstr{j}-{_h}\" d=\"{ef.D}\" fill=\"none\" stroke=\"{col}\" stroke-width=\"{Nm(_motion.RingStroke)}\" stroke-dasharray=\"{_strokes.StreamDash}\" opacity=\"0\"/>");
        }

        // working breathing rings (card bounds; the pulse expands via stroke-width). Gated off
        // wholesale by RingsEnabled=false, alongside the impact rings above.
        if (_motion.RingsEnabled)
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
            sb.Append($"<path class=\"beck-trail bt{i}-{_h}\" d=\"{p.D}\" fill=\"none\" stroke=\"{SvgWriter.Attr(p.Color)}\" stroke-width=\"{Nm(_motion.OverlayStroke)}\" ")
              .Append($"style=\"stroke-dasharray:{Nm(p.Length)};stroke-dashoffset:{Nm(off)}\"/>");
        }
        // packet dots
        for (int i = 0; i < _s.Packets.Count; i++)
        {
            PacketHop p = _s.Packets[i];
            string fillStroke = p.Shape == PacketShape.Ring
                ? $"fill=\"none\" stroke=\"{SvgWriter.Attr(p.Color)}\" stroke-width=\"{Nm(Math.Max(_motion.PacketRingMin, p.Size * _motion.PacketRingFactor))}\""
                : $"fill=\"{SvgWriter.Attr(p.Color)}\"";
            bool glowOn = p.Glow && _motion.GlowEnabled;
            string glow = glowOn ? $" filter=\"url(#beck-glow-{_h})\"" : "";
            if (glowOn) _needGlow = true;
            // Square (terminal's "block packet" glyph): a centred <rect> riding the same offset-path
            // instead of a <circle> — the shape is style data (StyleMotion.PacketGlyph), the markup
            // swap is the only branch this adds. Centred on the offset-path's own origin (no
            // offset-anchor — see the packet-label comment below for why) via x/y=-size.
            if (p.Shape == PacketShape.Square)
                sb.Append($"<rect class=\"beck-packet bp{i}-{_h}\" x=\"{Nm(-p.Size)}\" y=\"{Nm(-p.Size)}\" width=\"{Nm(2 * p.Size)}\" height=\"{Nm(2 * p.Size)}\" {fillStroke}{glow} opacity=\"0\" ")
                  .Append($"style=\"offset-path:path('{p.D}');offset-rotate:0deg\"/>");
            // Train (metro's identity glyph): an elongated rounded-rect capsule centred on the offset
            // point, its long axis running ALONG the path — the one packet that rotates with the route.
            // offset-rotate:auto aligns the local +x axis with the path tangent (CSS default), so the
            // capsule leans into curves like a carriage; every other glyph pins offset-rotate:0deg upright.
            // Half-height = p.Size; the capsule runs 3.4×size long with fully-rounded (stadium) ends.
            else if (p.Shape == PacketShape.Train)
            {
                double len = p.Size * 3.4, ht = p.Size * 2;
                sb.Append($"<rect class=\"beck-packet bp{i}-{_h}\" x=\"{Nm(-len / 2)}\" y=\"{Nm(-ht / 2)}\" width=\"{Nm(len)}\" height=\"{Nm(ht)}\" rx=\"{Nm(ht / 2)}\" {fillStroke}{glow} opacity=\"0\" ")
                  .Append($"style=\"offset-path:path('{p.D}');offset-rotate:auto\"/>");
            }
            else
                sb.Append($"<circle class=\"beck-packet bp{i}-{_h}\" r=\"{Nm(p.Size)}\" {fillStroke}{glow} opacity=\"0\" ")
                  .Append($"style=\"offset-path:path('{p.D}');offset-rotate:0deg\"/>");
            // No offset-anchor here: Chromium mispositions SVG <text> with `offset-anchor`
            // (it lands at large negative coordinates). text-anchor="middle" centres the
            // label on the offset point; translateY lifts it above the dot (packet.ts).
            if (!string.IsNullOrEmpty(p.Label))
                sb.Append($"<text class=\"beck-packet-label bpl{i}-{_h}\" text-anchor=\"middle\" fill=\"{SvgWriter.Attr(p.Color)}\" opacity=\"0\" ")
                  .Append($"style=\"offset-path:path('{p.D}');offset-rotate:0deg;transform:translateY(-{Nm(p.Size + 6)}px)\">{SvgWriter.Text(p.Label!)}</text>");
        }
        return sb.Append("</g>").ToString();
    }

    /// <summary>
    /// Compiles the per-edge overlay layer (<see cref="EdgeOverlaySpec"/>) into CSS: each overlay path
    /// runs a self-contained <c>linear infinite</c> loop over <see cref="StyleEdges.OverlayPeriod"/> —
    /// the same compiled, delay-chain-free discipline as the flow <c>stream</c> march — with every
    /// keyframe stop baked (comet phase folded into the starting <c>stroke-dashoffset</c>, never an
    /// <c>animation-delay</c>). Returns <c>""</c> when there are no overlays (classic), so the motion
    /// block stays byte-identical. The caller wraps the result in the reduced-motion guard, so a
    /// reduced-motion / static viewer sees only the overlay's baked resting frame.
    /// </summary>
    public static string EdgeOverlayCss(string hash, IReadOnlyList<EdgeOverlaySpec> overlays, StyleEdges edges)
    {
        if (overlays.Count == 0) return "";
        var sb = new StringBuilder();
        string dur = Nm(edges.OverlayPeriod);
        // Timing: linear (classic — glow's smooth comet, a gliding march) unless OverlaySteps is set,
        // which ratchets the overlay in n hard jumps per cycle (brutalist / terminal's mechanical tick)
        // via the same stepped-ease emitter PacketSteps/TrailSteps use. Draw-on's eased wipe reads as a
        // smooth ink, so it always stays linear.
        string? stepsTiming = edges.OverlaySteps is int n ? Easing.ToCss(Easing.StepsN(n)) : null;
        foreach (EdgeOverlaySpec o in overlays)
        {
            string cls = $"beo{o.Index}-{hash}", kf = $"kbeo{o.Index}-{hash}";
            string timing = stepsTiming != null && o.Mode != EdgeOverlay.DrawOn ? stepsTiming : "linear";
            sb.Append($".b-{hash} .{cls}{{animation:{kf} {dur}s {timing} infinite;}}");
            sb.Append($"@keyframes {kf}{{");
            switch (o.Mode)
            {
                case EdgeOverlay.Comet:
                {
                    // A single lit dash (CometDash) over a full-length gap sweeps one dot end-to-end; the
                    // per-edge phase is baked into the start offset so comets on different edges are out of
                    // step without any delay chain.
                    double span = edges.CometDash + o.Length;
                    double start = o.Phase * span;
                    sb.Append($"from{{stroke-dashoffset:{Nm(start)};}}to{{stroke-dashoffset:{Nm(start - span)};}}");
                    break;
                }
                case EdgeOverlay.DrawOn:
                    // Wipe from fully hidden (offset = len) to fully drawn, hold, then reset each period.
                    sb.Append($"0%{{stroke-dashoffset:{Nm(o.Length)};}}55%{{stroke-dashoffset:0;}}")
                      .Append($"82%{{stroke-dashoffset:0;}}100%{{stroke-dashoffset:{Nm(o.Length)};}}");
                    break;
                default: // Marching
                    sb.Append($"from{{stroke-dashoffset:0;}}to{{stroke-dashoffset:{Nm(-2 * edges.CometDash)};}}");
                    break;
            }
            sb.Append('}');
        }
        return sb.ToString();
    }

    /// <summary>The glow filter def (only if a packet needs it — call after Markup()).</summary>
    public string Defs() => _needGlow
        ? $"<filter id=\"beck-glow-{_h}\" x=\"-200%\" y=\"-200%\" width=\"500%\" height=\"500%\">"
          + $"<feGaussianBlur in=\"SourceGraphic\" stdDeviation=\"{Nm(_motion.PacketGlowBlur)}\" result=\"blur\"/>"
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
                // Process every swap, including the trailing restore-to-0 entry — it must still
                // emit its keyframe so the pill resets at a mid-flow restore, not just the cycle end.
                for (int k = 0; k < sw.Count; k++)
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
    private void SequenceChoreoCss(StringBuilder sb)
    {
        if (_choreo is null) return;
        double finaleAt = Math.Max(0, _s.Duration - 0.75);

        // initial dims (the whole block is motion-guarded by the caller).
        sb.Append($".b-{_h} .beck-msg path{{opacity:{G(_motion.DimLine)};}}");
        sb.Append($".b-{_h} .beck-msg-chip,.b-{_h} .beck-msg-text{{opacity:{G(_motion.DimLabel)};}}");
        sb.Append($".b-{_h} .beck-band{{opacity:{G(_motion.DimBand)};}}");
        sb.Append($".b-{_h} .beck-activation{{opacity:{G(_motion.DimAct)};}}");

        // first departure + arrival per message edge.
        var revealAt = new Dictionary<string, (double At, double Arr)>();
        foreach (PacketHop p in _s.Packets)
            if (p.EdgeId is { } id && !revealAt.ContainsKey(id)) revealAt[id] = (p.Start, p.Start + p.Duration);

        // Reveal ramp lengths, scaled by the style's SequenceRevealScale (1.0 = classic exact:
        // 0.25s row/label, 0.4s band). A style > 1 (editorial) draws the scenery on slowly + softly.
        double rowDur = 0.25 * _motion.SequenceRevealScale;
        double bandDur = 0.4 * _motion.SequenceRevealScale;

        int idx = 0;
        foreach (var (id, t) in revealAt)
        {
            string esc = id.Replace("\"", "\\\"");
            RevealTrack(sb, $"kchl{idx}-{_h}", $".b-{_h} .beck-msg[data-msg=\"{esc}\"] path", _motion.DimLine, t.At, rowDur, finaleAt);
            RevealTrack(sb, $"kcht{idx}-{_h}",
                $".b-{_h} .beck-msg[data-msg=\"{esc}\"] .beck-msg-chip,.b-{_h} .beck-msg[data-msg=\"{esc}\"] .beck-msg-text",
                _motion.DimLabel, t.At, rowDur, finaleAt);
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
            RevealTrack(sb, $"kchb{i}-{_h}", $".b-{_h} .beck-band[data-band=\"{i}\"]", _motion.DimBand, _s.Phases[i], bandDur, finaleAt);
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
        sb.Append($"@keyframes {kf}{{0%{{opacity:{G(_motion.DimAct)};}}");
        if (startSec is { } ss)
        {
            double s = Pct(ss), se = Pct(ss + 0.3);
            if (s > e) sb.Append($"{P(s - e)}%{{opacity:{G(_motion.DimAct)};}}");
            sb.Append($"{P(s)}%{{opacity:{G(_motion.DimAct)};}}{P(se)}%{{opacity:1;}}");
        }
        double fadeAt = endSec ?? finaleSec;
        double fs = Pct(fadeAt), fe = Pct(fadeAt + 0.35);
        sb.Append($"{P(fs)}%{{opacity:1;}}{P(fe)}%{{opacity:{G(_motion.DimAct)};}}");
        sb.Append($"100%{{opacity:{G(_motion.DimAct)};}}}}");
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
        if (!_motion.RingsEnabled || _s.Working.Count == 0) return;
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
        sb.Append($"@keyframes bbreath-{_h}{{0%{{stroke-width:0;stroke-opacity:{Nm(0.55 * _motion.EffectAmplitude)};}}100%{{stroke-width:{Nm(18 * _motion.EffectAmplitude)};stroke-opacity:0;}}}}");
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
            // Brutalist's stepped flow motion: the packet hops its edge in n discrete jumps
            // (StyleMotion.PacketSteps) instead of its smooth per-edge-kind ease. The trail below
            // inherits this stepped ease unless a distinct TrailSteps is set. null → classic ease.
            Ease packetEase = _motion.PacketSteps is { } pn ? Easing.StepsN(pn) : p.Ease;
            string ease = Easing.ToCss(packetEase);
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

            // trail: reveal then hold, snap back at restore. A style's TrailSteps (terminal's
            // "hard-step trails") swaps the reveal's timing-function for a blocky steps(n); the
            // travelling packet glyph above keeps its own per-edge-kind ease unchanged.
            double off = p.Reversed ? -p.Length : p.Length;
            string trailEase = _motion.TrailSteps is { } trailN ? Easing.ToCss(Easing.StepsN(trailN)) : ease;
            sb.Append($".b-{_h} .bt{i}-{_h}{{animation:kt{i}-{_h} {_cyc};}}");
            sb.Append($"@keyframes kt{i}-{_h}{{");
            sb.Append($"0%{{stroke-dashoffset:{Nm(off)};}}");
            sb.Append($"{P(ws)}%{{stroke-dashoffset:{Nm(off)};animation-timing-function:{trailEase};}}");
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
            else GlowCss(sb, j, c.Start, c.Kind == CardFxKind.Highlight ? 0.21 : 0.12, c.Kind == CardFxKind.Highlight ? _motion.HighlightDur : _motion.FailDur);
        }

        // impact rings (gated off with the markup by RingsEnabled=false)
        if (_motion.RingsEnabled)
            for (int j = 0; j < _s.Impacts.Count; j++) ImpactCss(sb, j, _s.Impacts[j].Start);
    }

    /// <summary>
    /// The pulse/highlight peak transform. Classic lifts the card (<c>translateY(-2px) scale(1.04)</c>);
    /// a <see cref="StyleMotion.PressDown"/> style (extrude's 2.5D slabs) instead presses it down-right
    /// toward its depth faces (<c>translate(2px,2px)</c>), so the slab reads as pushed into the page.
    /// Both compile into the same shared-cycle transform keyframes.
    /// </summary>
    private string ActivePeak => _motion.PressDown ? "translate(2px,2px)" : "translateY(-2px) scale(1.04)";

    private void TransformTrack(StringBuilder sb, int node, List<CardFx> list)
    {
        var pts = new List<(double T, string Tf, string? EaseCss)> { (0, "none", null) };
        foreach (CardFx c in list.OrderBy(c => c.Start))
        {
            double s = c.Start;
            switch (c.Kind)
            {
                case CardFxKind.Pulse:
                    pts.Add((s, "none", PulseInCss));
                    pts.Add((s + 0.18, ActivePeak, PulsePeakCss));
                    pts.Add((s + _motion.PulseDur, "none", null));
                    break;
                case CardFxKind.Highlight:
                    pts.Add((s, "none", HighlightInCss));
                    pts.Add((s + 0.21, ActivePeak, HighlightPeakCss));
                    pts.Add((s + _motion.HighlightDur, "none", null));
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
        foreach (var (t, tf, easeCss) in pts)
        {
            string pct = P(Pct(t));
            if (pct == lastPct) continue; // collapse coincident keyframes (last effect wins)
            lastPct = pct;
            sb.Append($"{pct}%{{transform:{tf};");
            if (easeCss is not null) sb.Append($"animation-timing-function:{easeCss};");
            sb.Append('}');
        }
        sb.Append('}');
    }

    private void RippleCss(StringBuilder sb, int j, double start)
    {
        double s = Pct(start), end = Pct(start + 0.48), e = 0.01;
        string po = Power2OutCss;
        sb.Append($".b-{_h} .brip{j}-{_h}{{animation:krip{j}-{_h} {_cyc};}}");
        sb.Append($"@keyframes krip{j}-{_h}{{");
        sb.Append("0%{opacity:0;transform:scale(1);}");
        if (s > e) sb.Append($"{P(s - e)}%{{opacity:0;transform:scale(1);}}");
        sb.Append($"{P(s)}%{{opacity:{Nm(0.6 * _motion.EffectAmplitude)};transform:scale(1);animation-timing-function:{po};}}");
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
        sb.Append($"{P(up)}%{{opacity:{Nm(_motion.EffectAmplitude)};}}");
        sb.Append($"{P(hold)}%{{opacity:{Nm(_motion.EffectAmplitude)};}}");
        if (hold + e < 100) sb.Append($"{P(hold + e)}%{{opacity:0;}}");
        sb.Append("100%{opacity:0;}}");
    }

    private void ImpactCss(StringBuilder sb, int j, double start)
    {
        double s = Pct(start), end = Pct(start + 0.55), e = 0.01;
        string po = Power2OutCss;
        // stroke-width sweeps from the style's ring stroke down to a fifth of it (classic:
        // 2.5 -> 0.5) as the ring expands — proportional to RingStroke so a thinner style ring
        // sweeps thinner too.
        string wStart = Nm(_motion.RingStroke), wEnd = Nm(_motion.RingStroke * 0.2);
        string peakOpacity = Nm(0.9 * _motion.EffectAmplitude);
        sb.Append($".b-{_h} .bimp{j}-{_h}{{animation:kimp{j}-{_h} {_cyc};}}");
        sb.Append($"@keyframes kimp{j}-{_h}{{");
        sb.Append($"0%{{opacity:0;transform:scale(1);stroke-width:{wStart};}}");
        if (s > e) sb.Append($"{P(s - e)}%{{opacity:0;transform:scale(1);}}");
        sb.Append($"{P(s)}%{{opacity:{peakOpacity};transform:scale(1);stroke-width:{wStart};animation-timing-function:{po};}}");
        sb.Append($"{P(end)}%{{opacity:0;transform:scale(3.4);stroke-width:{wEnd};}}");
        sb.Append($"100%{{opacity:0;transform:scale(1);stroke-width:{wStart};}}}}");
    }
}
