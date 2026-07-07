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
    private readonly StringBuilder _defs = new();
    private readonly Dictionary<string, string> _actFills = new();
    private int _gradSeq;

    public SequencePainter(string hash, Markers markers, ITextMeasurer measurer)
    {
        _hash = hash;
        _markers = markers;
        _m = measurer;
    }

    /// <summary>Gradient defs (appended to the document <c>&lt;defs&gt;</c>).</summary>
    public string Defs => _defs.ToString();

    /// <summary>Message paths, for the flow schedule (each packet rides a message row).</summary>
    public List<FlowEdge> MessageEdges { get; } = new();

    private static string N(double n) => SvgWriter.Num(n);
    private static string I(double n) => Js.Str(Js.Round(n));

    public string Render(DiagramModel model, SequenceLayoutResult layout)
    {
        var sb = new StringBuilder();

        // ---- section bands (behind everything) ----
        for (int i = 0; i < layout.Bands.Count; i++)
        {
            SectionBand b = layout.Bands[i];
            sb.Append($"<g class=\"beck-band\" data-band=\"{i}\" style=\"--beck-accent:{SvgWriter.Attr(b.Accent)}\">");
            sb.Append($"<rect class=\"beck-band-box\" x=\"{N(b.X)}\" y=\"{N(b.Y)}\" width=\"{N(b.W)}\" height=\"{N(b.H)}\" rx=\"14\"/>");
            double lw = _m.Measure(b.Label, FontRole.BandLabel).Width;
            sb.Append(Chip(b.X + 24 + lw / 2, b.Y, b.Label.ToUpperInvariant(), lw, "beck-band-chip", "beck-band-label", 10, 4,
                "font-family:var(--beck-font-mono)", 9.92, 700, "letter-spacing:0.14em"));
            sb.Append("</g>");
        }

        // ---- lifelines ----
        double cardBottom = model.Nodes.Min(p => layout.Nodes[p.Id].Y + layout.Nodes[p.Id].H);
        string stroke = Lifeline(cardBottom, layout.LifelineBottom);
        foreach (var p in model.Nodes)
        {
            double cx = layout.Centers[p.Id];
            Rect card = layout.Nodes[p.Id];
            sb.Append($"<line class=\"beck-lifeline\" x1=\"{N(cx)}\" y1=\"{N(card.Y + card.H + 2)}\" x2=\"{N(cx)}\" y2=\"{N(layout.LifelineBottom)}\" style=\"stroke:{stroke}\"/>");
        }

        // ---- activation bars ----
        for (int bi = 0; bi < layout.Activations.Count; bi++)
        {
            ActivationBar b = layout.Activations[bi];
            double cx = layout.Centers[b.Participant];
            double x = cx - SequenceLayout.BarHalf + b.Level * SequenceLayout.LevelStep;
            double h = Math.Max(SequenceLayout.BarHalf * 2, b.Y2 - b.Y1);
            sb.Append($"<rect class=\"beck-activation\" data-bar=\"{bi}\" x=\"{N(x)}\" y=\"{N(b.Y1)}\" width=\"{N(SequenceLayout.BarHalf * 2)}\" height=\"{N(h)}\" rx=\"{N(SequenceLayout.BarHalf)}\" ")
              .Append($"fill=\"{ActivationFill(b.Accent)}\" style=\"--beck-accent:{SvgWriter.Attr(b.Accent)}\" data-start=\"{SvgWriter.Attr(b.StartEdge)}\" data-end=\"{SvgWriter.Attr(b.EndEdge)}\"/>");
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
                MessageEdges.Add(new FlowEdge(edge.Id, edge.From, edge.To, edge.Kind, selfD));
                if (!string.IsNullOrEmpty(edge.Label))
                    sb.Append($"<text class=\"beck-msg-text beck-msg-text--bare\" x=\"{N(cxFrom + SequenceLayout.SelfLoop + 12)}\" y=\"{N(row.Y + 11)}\" text-anchor=\"start\" dominant-baseline=\"central\" font-size=\"10.88\" font-weight=\"500\" style=\"font-family:var(--beck-font-mono)\">{SvgWriter.Text(edge.Label!)}</text>");
            }
            else
            {
                double dir = Math.Sign(cxTo - cxFrom); if (dir == 0) dir = 1;
                double x1 = cxFrom + dir * SequenceLayout.ActivationOffset(layout.Activations, edge.From, row.Y);
                double x2 = cxTo - dir * SequenceLayout.ActivationOffset(layout.Activations, edge.To, row.Y);
                string msgD = $"M {N(x1)} {N(row.Y)} L {N(x2)} {N(row.Y)}";
                sb.Append(MsgPath(edge, msgD));
                MessageEdges.Add(new FlowEdge(edge.Id, edge.From, edge.To, edge.Kind, msgD));
                if (!string.IsNullOrEmpty(edge.Label))
                {
                    double mw = _m.Measure(edge.Label!, FontRole.MsgText).Width;
                    sb.Append(Chip((x1 + x2) / 2, row.Y - 17, edge.Label!, mw, "beck-msg-chip", "beck-msg-text", 10, 4,
                        "font-family:var(--beck-font-mono)", 10.88, 500, null));
                }
            }
            sb.Append("</g>");
        }

        return sb.ToString();
    }

    private string MsgPath(EdgeModel edge, string d)
    {
        var sb = new StringBuilder();
        sb.Append($"<path class=\"beck-edge beck-edge--{Tokens.EdgeKind.Wire(edge.Kind)}\" d=\"{d}\" style=\"stroke:{SvgWriter.Attr(edge.Color)}\" stroke-width=\"2\"");
        if (edge.Style == EdgeStyle.Dashed) sb.Append(" stroke-dasharray=\"7 5\"");
        if ((edge.MarkerEnd ?? (edge.Arrow is ArrowEnds.End or ArrowEnds.Both ? MarkerShape.Arrow : (MarkerShape?)null)) is { } m)
            sb.Append($" marker-end=\"url(#{_markers.Ensure(edge.Color, m)})\"");
        sb.Append($" data-edge=\"{SvgWriter.Attr(edge.Id)}\"/>");
        return sb.ToString();
    }

    private static string Chip(double cx, double cy, string text, double w, string chipCls, string textCls,
        double padX, double padY, string fontStyle, double size, int weight, string? extraStyle)
    {
        double h = 12; // getBBox height fallback
        double bw = w + padX * 2, bh = h + padY * 2;
        string ls = extraStyle != null ? ";" + extraStyle : "";
        return $"<rect class=\"{chipCls}\" x=\"{I(cx - bw / 2)}\" y=\"{I(cy - bh / 2)}\" width=\"{I(bw)}\" height=\"{I(bh)}\" rx=\"{SvgWriter.Num(bh / 2)}\"/>"
             + $"<text class=\"{textCls}\" x=\"{I(cx)}\" y=\"{I(cy)}\" text-anchor=\"middle\" dominant-baseline=\"central\" font-size=\"{SvgWriter.Num(size)}\" font-weight=\"{weight}\" style=\"{fontStyle}{ls}\">{SvgWriter.Text(text)}</text>";
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
