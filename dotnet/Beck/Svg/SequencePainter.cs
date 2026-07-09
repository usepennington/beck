using System.Text;
using Beck.Rendering.Animate;
using Beck.Rendering.Route;
using Beck.Rendering.Text;

namespace Beck.Rendering.Svg;

/// <summary>
/// Emits the sequence-diagram scenery + messages — a port of
/// <c>src/route/sequence.ts</c>: section bands, faded lifelines, activation bars,
/// and one <c>&lt;g class="beck-msg"&gt;</c> per message (straight line or a
/// rounded self-loop) with its pill label. <c>getBBox</c> chip sizing → the
/// measurer at the message/band roles.
/// </summary>
internal sealed class SequencePainter
{
    private readonly string _hash;
    private readonly Markers _markers;
    private readonly ITextMeasurer _m;
    private readonly BeckStyle _style;
    private readonly StringBuilder _defs = new();
    private readonly Dictionary<string, string> _actFills = new();
    private int _gradSeq;
    private bool _motion;
    private int _overlaySeq;

    public SequencePainter(string hash, Markers markers, ITextMeasurer measurer, BeckStyle style)
    {
        _hash = hash;
        _markers = markers;
        _m = measurer;
        _style = style;
    }

    /// <summary>Gradient defs (appended to the document <c>&lt;defs&gt;</c>).</summary>
    public string Defs => _defs.ToString();

    /// <summary>Message paths, for the flow schedule (each packet rides a message row).</summary>
    public List<FlowEdge> MessageEdges { get; } = new();

    /// <summary>Per-message overlay specs (comet/draw-on/marching), compiled to motion CSS by the caller.</summary>
    public List<EdgeOverlaySpec> Overlays { get; } = new();

    private static string N(double n) => SvgWriter.Num(n);
    private static string I(double n) => Js.Str(Js.Round(n));
    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string Render(DiagramModel model, SequenceLayoutResult layout, bool motion = false)
    {
        _motion = motion;
        var sb = new StringBuilder();
        FontRoleSpec bandSpec = _style.Typography.Roles.Of(FontRole.BandLabel);
        FontRoleSpec msgSpec = _style.Typography.Roles.Of(FontRole.MsgText);

        // ---- section bands (behind everything) ----
        for (int i = 0; i < layout.Bands.Count; i++)
        {
            SectionBand b = layout.Bands[i];
            sb.Append($"<g class=\"beck-band\" data-band=\"{i}\" style=\"--beck-accent:{SvgWriter.Attr(b.Accent)}\">");
            sb.Append($"<rect class=\"beck-band-box\" x=\"{N(b.X)}\" y=\"{N(b.Y)}\" width=\"{N(b.W)}\" height=\"{N(b.H)}\" rx=\"{N(_style.Geometry.BandRadius)}\"/>");
            double lw = _m.Measure(b.Label, FontRole.BandLabel, bandSpec).Width;
            sb.Append(Chip(b.X + 24 + lw / 2, b.Y, b.Label.ToUpperInvariant(), lw, "beck-band-chip", "beck-band-label", 10, 4,
                "font-family:var(--beck-font-mono)", bandSpec.SizePx, bandSpec.Weight, $"letter-spacing:{SvgWriter.Ls(bandSpec.LetterSpacingEm)}"));
            sb.Append("</g>");
        }

        // ---- lifelines ----
        double cardBottom = model.Nodes.Min(p => layout.Nodes[p.Id].Y + layout.Nodes[p.Id].H);
        string stroke = Lifeline(cardBottom, layout.LifelineBottom);
        foreach (var p in model.Nodes)
        {
            double cx = layout.Centers[p.Id];
            Rect card = layout.Nodes[p.Id];
            double y1 = card.Y + card.H + 2, y2 = layout.LifelineBottom;
            // Lifeline treatment (StyleEdges.Lifeline): classic Dashed + FaintSolid are the straight
            // <line> (the dash on/off is a CSS concern); Wobbly swaps to a single sideways-bowed <path>
            // with its endpoints preserved. Classic is byte-identical.
            if (_style.Edges.Lifeline == LifelineShape.Wobbly)
            {
                string llD = Shaping.BowLine(cx, y1, cx, y2, Math.Max(_style.Edges.BowAmplitude, 2), _hash + ":ll:" + p.Id);
                sb.Append($"<path class=\"beck-lifeline\" d=\"{llD}\" fill=\"none\" style=\"stroke:{stroke}\"/>");
            }
            else
                sb.Append($"<line class=\"beck-lifeline\" x1=\"{N(cx)}\" y1=\"{N(y1)}\" x2=\"{N(cx)}\" y2=\"{N(y2)}\" style=\"stroke:{stroke}\"/>");
        }

        // ---- activation bars ----
        for (int bi = 0; bi < layout.Activations.Count; bi++)
        {
            ActivationBar b = layout.Activations[bi];
            double cx = layout.Centers[b.Participant];
            double x = cx - SequenceLayout.BarHalf + b.Level * SequenceLayout.LevelStep;
            double h = Math.Max(SequenceLayout.BarHalf * 2, b.Y2 - b.Y1);
            // Activation fill: classic is a vertical accent gradient (with a CSS bloom filter). Sketch (§1b)
            // redraws the bar as a hand-drawn OUTLINED rect — a translucent accent fill + accent stroke —
            // and drops the bloom (mix.ActivationGlow = 0 makes the `.beck-activation` drop-shadow
            // transparent). Both keep the same rect/class/data-* so the flow reveal + dimming choreography
            // are untouched; classic emits the exact historical attribute → byte-identical.
            string actAccent = SvgWriter.Attr(b.Accent);
            string actFill, actStroke;
            if (_style.Artwork == StyleArtwork.Sketch)
            {
                actFill = "";
                actStroke = $";fill:color-mix(in srgb, var(--beck-accent) 20%, transparent);stroke:var(--beck-accent);stroke-width:{N(_style.Geometry.HairlineStroke)}";
            }
            else
            {
                actFill = $"fill=\"{ActivationFill(b.Accent)}\" ";
                actStroke = "";
            }
            sb.Append($"<rect class=\"beck-activation\" data-bar=\"{bi}\" x=\"{N(x)}\" y=\"{N(b.Y1)}\" width=\"{N(SequenceLayout.BarHalf * 2)}\" height=\"{N(h)}\" rx=\"{N(SequenceLayout.BarHalf)}\" ")
              .Append($"{actFill}style=\"--beck-accent:{actAccent}{actStroke}\" data-start=\"{SvgWriter.Attr(b.StartEdge)}\" data-end=\"{SvgWriter.Attr(b.EndEdge)}\"/>");
        }

        // ---- messages ----
        foreach (var row in layout.Rows)
        {
            EdgeModel edge = model.Edges[row.Index];
            double cxFrom = layout.Centers[edge.From], cxTo = layout.Centers[edge.To];
            sb.Append($"<g class=\"beck-msg{(edge.Reply ? " beck-msg--reply" : "")}\" data-msg=\"{SvgWriter.Attr(edge.Id)}\" style=\"--beck-accent:{SvgWriter.Attr(edge.Color)}\">");

            if (row.Self)
            {
                double off = SequenceLayout.ActivationOffset(layout.Activations, edge.From, row.Y);
                double sx = cxFrom + off;
                var poly = new List<Point>
                {
                    new(sx, row.Y), new(cxFrom + SequenceLayout.SelfLoop, row.Y),
                    new(cxFrom + SequenceLayout.SelfLoop, row.Y + 22), new(sx, row.Y + 22),
                };
                string selfD = StepRound.RoundedPath(poly, 9);
                sb.Append(MsgPath(edge, selfD));
                sb.Append(MsgOverlay(selfD));
                MessageEdges.Add(new FlowEdge(edge.Id, edge.From, edge.To, edge.Kind, selfD));
                // Metro station dots at the self-loop's two lifeline anchors (over the line).
                sb.Append(Artwork.Station(_style, sx, row.Y, edge.Color));
                sb.Append(Artwork.Station(_style, sx, row.Y + 22, edge.Color));
                if (!string.IsNullOrEmpty(edge.Label))
                    sb.Append($"<text class=\"beck-msg-text beck-msg-text--bare\" x=\"{N(cxFrom + SequenceLayout.SelfLoop + 12)}\" y=\"{N(row.Y + 11)}\" text-anchor=\"start\" dominant-baseline=\"central\" font-size=\"{N(msgSpec.SizePx)}\" font-weight=\"{P(msgSpec.Weight)}\" style=\"font-family:var(--beck-font-mono)\">{SvgWriter.Text(edge.Label!)}</text>");
            }
            else
            {
                double dir = Math.Sign(cxTo - cxFrom); if (dir == 0) dir = 1;
                double x1 = cxFrom + dir * SequenceLayout.ActivationOffset(layout.Activations, edge.From, row.Y);
                double x2 = cxTo - dir * SequenceLayout.ActivationOffset(layout.Activations, edge.To, row.Y);
                string msgD0 = $"M {N(x1)} {N(row.Y)} L {N(x2)} {N(row.Y)}";
                // Bow the straight message when the style opts in (endpoints preserved); classic → verbatim.
                string msgD = Shaping.EdgePath(_style, msgD0,
                    new[] { new Point(x1, row.Y), new Point(x2, row.Y) }, _hash + ":" + edge.Id);
                sb.Append(MsgPath(edge, msgD));
                sb.Append(MsgOverlay(msgD));
                MessageEdges.Add(new FlowEdge(edge.Id, edge.From, edge.To, edge.Kind, msgD));
                // Metro station dots at each message's two endpoints (over the line).
                sb.Append(Artwork.Station(_style, x1, row.Y, edge.Color));
                sb.Append(Artwork.Station(_style, x2, row.Y, edge.Color));
                if (!string.IsNullOrEmpty(edge.Label))
                {
                    double mw = _m.Measure(edge.Label!, FontRole.MsgText, msgSpec).Width;
                    sb.Append(Chip((x1 + x2) / 2, row.Y - 17, edge.Label!, mw, "beck-msg-chip", "beck-msg-text", 10, 4,
                        "font-family:var(--beck-font-mono)", msgSpec.SizePx, msgSpec.Weight, null));
                }
            }
            sb.Append("</g>");
        }

        return sb.ToString();
    }

    private string MsgPath(EdgeModel edge, string d)
    {
        StyleEdges es = _style.Edges;
        var sb = new StringBuilder();
        // Optional faint base-layer opacity (glow's dim rail under the bright comet) — matches the
        // architecture edge painter's BaseOpacity treatment. null → no attribute (classic, byte-identical).
        string baseOp = es.BaseOpacity is { } bo ? $";stroke-opacity:{SvgWriter.Num(bo)}" : "";
        sb.Append($"<path class=\"beck-edge beck-edge--{Tokens.EdgeKind.Wire(edge.Kind)}\" d=\"{d}\" style=\"stroke:{SvgWriter.Attr(edge.Color)}{baseOp}\" stroke-width=\"{SvgWriter.Num(_style.Geometry.MessageStroke)}\"");
        if (edge.Style == EdgeStyle.Dashed) sb.Append($" stroke-dasharray=\"{_style.Strokes.EdgeDash}\"");
        // Marker colour: the message's own colour, or the style's comet-hue override (glow) when the
        // message uses the default colour — a bright arrowhead over a faint slate base rail.
        string markerColor = es.MarkerColor is { } mkc && edge.Color == "var(--beck-edge)" ? mkc : edge.Color;
        if ((edge.MarkerEnd ?? (edge.Arrow is ArrowEnds.End or ArrowEnds.Both ? MarkerShape.Arrow : (MarkerShape?)null)) is { } m)
            sb.Append($" marker-end=\"url(#{_markers.Ensure(markerColor, m, _style.Edges, _style.Geometry.MessageStroke)})\"");
        sb.Append($" data-edge=\"{SvgWriter.Attr(edge.Id)}\"/>");
        return sb.ToString();
    }

    /// <summary>The optional per-message overlay layer (comet/draw-on/marching) sharing the message's
    /// exact <paramref name="d"/> — an additional sibling path, never a split. Emitted only when motion
    /// is live and the style opts in; classic (Overlay=None) returns <c>""</c> — byte-identical.</summary>
    private string MsgOverlay(string d)
    {
        if (!_motion || _style.Edges.Overlay == EdgeOverlay.None) return "";
        var (markup, spec) = SvgRenderer.OverlayPath(_style, d, _overlaySeq++, _hash);
        Overlays.Add(spec);
        return markup;
    }

    private static string Chip(double cx, double cy, string text, double w, string chipCls, string textCls,
        double padX, double padY, string fontStyle, double size, int weight, string? extraStyle)
    {
        double h = 12; // getBBox height fallback
        double bw = w + padX * 2, bh = h + padY * 2;
        string ls = extraStyle != null ? ";" + extraStyle : "";
        return $"<rect class=\"{chipCls}\" x=\"{I(cx - bw / 2)}\" y=\"{I(cy - bh / 2)}\" width=\"{I(bw)}\" height=\"{I(bh)}\" rx=\"{SvgWriter.Num(bh / 2)}\"/>"
             + $"<text class=\"{textCls}\" x=\"{I(cx)}\" y=\"{I(cy)}\" text-anchor=\"middle\" dominant-baseline=\"central\" font-size=\"{SvgWriter.Num(size)}\" font-weight=\"{P(weight)}\" style=\"{fontStyle}{ls}\">{SvgWriter.Text(text)}</text>";
    }

    private string Lifeline(double y1, double y2)
    {
        string id = $"beck-fade-{_gradSeq++}-{_hash}";
        _defs.Append($"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" x1=\"0\" y1=\"{N(y1)}\" x2=\"0\" y2=\"{N(y2)}\">")
             .Append("<stop offset=\"0\" stop-opacity=\"1\" style=\"stop-color:var(--beck-edge)\"/>")
             .Append("<stop offset=\"0.8\" stop-opacity=\"1\" style=\"stop-color:var(--beck-edge)\"/>")
             .Append("<stop offset=\"1\" stop-opacity=\"0\" style=\"stop-color:var(--beck-edge)\"/>")
             .Append("</linearGradient>");
        return $"url(#{id})";
    }

    private string ActivationFill(string accent)
    {
        if (_actFills.TryGetValue(accent, out string? hit)) return hit;
        string id = $"beck-act-{_gradSeq++}-{_hash}";
        _defs.Append($"<linearGradient id=\"{id}\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\" style=\"--beck-accent:{SvgWriter.Attr(accent)}\">")
             .Append("<stop offset=\"0\" stop-opacity=\"0.95\" style=\"stop-color:var(--beck-accent)\"/>")
             .Append("<stop offset=\"1\" stop-opacity=\"0.35\" style=\"stop-color:var(--beck-accent)\"/>")
             .Append("</linearGradient>");
        string url = $"url(#{id})";
        _actFills[accent] = url;
        return url;
    }
}
