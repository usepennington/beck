using System.Text;
using Beck.Rendering.Animate;
using Beck.Rendering.Route;
using Beck.Rendering.Text;

namespace Beck.Rendering.Svg;

/// <summary>
/// Assembles the static SVG document for a diagram: theming <c>&lt;style&gt;</c>,
/// marker <c>&lt;defs&gt;</c>, title block, and the layered z-order (groups →
/// edges → nodes → group labels). §8. Animation (§9–10) lands in M8/M9.
/// </summary>
internal static class SvgRenderer
{
    private static string N(double n) => SvgWriter.Num(n);

    public static string Render(DiagramModel model, ITextMeasurer measurer, string hash, SvgRenderOptions options)
    {
        var sizes = model.Nodes.ToDictionary(n => n.Id, n => CardSizer.Measure(n, measurer));
        var markers = new Markers(hash);
        // Pill states a node's flow will swap through (null unless animating with >1 state).
        var statusMap = StatusStates.Build(model);
        bool animating = options.Animation is AnimationMode.Full or AnimationMode.Scrub && model.Meta.Animate;
        IReadOnlyList<(string Text, string Color)>? StatesFor(string id) =>
            animating && statusMap.TryGetValue(id, out var s) && s.Count > 1 ? s : null;
        string extraDefs = "";
        LayoutResult layout;
        var body = new StringBuilder();
        var flowEdges = new List<FlowEdge>();
        SeqChoreo? choreo = null;

        if (model.Meta.Type == DiagramType.Sequence)
        {
            SequenceLayoutResult seq = SequenceLayout.Compute(model, sizes);
            layout = seq.AsLayout();
            var painter = new SequencePainter(hash, markers, measurer);
            body.Append("<g class=\"beck-overlay\">").Append(painter.Render(model, seq)).Append("</g>");
            extraDefs = painter.Defs;
            flowEdges = painter.MessageEdges;
            // Sequence storytelling applies only to a DERIVED flow (authored message order).
            if (model.Flow.Derived)
                choreo = new SeqChoreo(
                    seq.Activations.Select(a => (a.StartEdge ?? "", a.EndEdge ?? "")).ToList(),
                    seq.Bands.Count);
            body.Append("<g class=\"beck-nodes\">");
            for (int i = 0; i < model.Nodes.Count; i++)
                if (layout.Nodes.TryGetValue(model.Nodes[i].Id, out var r)) body.Append(Node(model.Nodes[i], r, measurer, hash, i, StatesFor(model.Nodes[i].Id)));
            body.Append("</g>");
        }
        else
        {
            layout = LayeredLayout.Compute(model, sizes);
            var edges = EdgePainter.RouteEdges(model, layout);
            flowEdges = edges.Select(e => new FlowEdge(e.Edge.Id, e.Edge.From, e.Edge.To, e.Edge.Kind, e.D)).ToList();

            // z1 groups (largest-area first so nested boxes stack on top)
            body.Append("<g class=\"beck-groups\">");
            foreach (var g in model.Groups.Where(g => layout.Groups.ContainsKey(g.Id))
                         .OrderByDescending(g => layout.Groups[g.Id].W * layout.Groups[g.Id].H))
            {
                Rect r = layout.Groups[g.Id];
                body.Append($"<rect class=\"beck-group\" x=\"{N(r.X)}\" y=\"{N(r.Y)}\" width=\"{N(r.W)}\" height=\"{N(r.H)}\" rx=\"18\" ")
                    .Append($"style=\"stroke:color-mix(in srgb, {SvgWriter.Attr(g.Accent)} 45%, transparent)\"/>");
            }
            body.Append("</g>");

            // z2 edges (+ markers), then labels (pass 2 — mid labels dodge every line)
            body.Append("<g class=\"beck-overlay\">");
            foreach (var e in edges) body.Append(Edge(e, markers));
            var placer = new LabelPlacer(layout.Nodes.Values, layout.Width, layout.Height);
            foreach (var e in edges)
            {
                if (!string.IsNullOrEmpty(e.Edge.FromLabel)) body.Append(placer.EndLabel(e.Points, e.Edge.FromLabel!, true, measurer));
                if (!string.IsNullOrEmpty(e.Edge.ToLabel)) body.Append(placer.EndLabel(e.Points, e.Edge.ToLabel!, false, measurer));
            }
            for (int i = 0; i < edges.Count; i++)
            {
                if (string.IsNullOrEmpty(edges[i].Edge.Label)) continue;
                var otherLines = edges.Where((_, j) => j != i).Select(o => (IReadOnlyList<Point>)o.Points).ToList();
                body.Append(placer.MidLabel(edges[i].Points, edges[i].Edge.Label!, otherLines, measurer));
            }
            body.Append("</g>");

            // z3 nodes
            body.Append("<g class=\"beck-nodes\">");
            for (int i = 0; i < model.Nodes.Count; i++)
                if (layout.Nodes.TryGetValue(model.Nodes[i].Id, out var r)) body.Append(Node(model.Nodes[i], r, measurer, hash, i, StatesFor(model.Nodes[i].Id)));
            body.Append("</g>");

            // z4 group labels
            body.Append("<g class=\"beck-group-labels\">");
            foreach (var g in model.Groups.Where(g => layout.Groups.ContainsKey(g.Id) && !string.IsNullOrEmpty(g.Label)))
            {
                Rect r = layout.Groups[g.Id];
                double lw = measurer.Measure(g.Label, FontRole.GroupLabel).Width;
                double lx = r.X + 14, ly = r.Y;
                body.Append($"<rect class=\"beck-group-label-bg\" x=\"{N(lx - 5.6)}\" y=\"{N(ly - 8)}\" width=\"{N(lw + 11.2)}\" height=\"16\" rx=\"3\"/>");
                body.Append($"<text class=\"beck-group-label\" x=\"{N(lx)}\" y=\"{N(ly)}\" dominant-baseline=\"central\" ")
                    .Append($"font-size=\"11.2\" font-weight=\"600\" letter-spacing=\"0.04em\" style=\"fill:{SvgWriter.Attr(g.Accent)}\">{SvgWriter.Text(g.Label.ToUpperInvariant())}</text>");
            }
            body.Append("</g>");
        }

        double titleH = TitleHeight(model);
        double w = layout.Width;
        double bodyH = layout.Height;

        // Narration caption bar (§8.6): a teleprompter under the diagram whose beats
        // cross-fade as the story plays. Rendered here (the compiler animates the beats).
        var beats = model.Flow.Steps.OfType<NarrateStep>().ToList();
        bool narrationActive = options.Animation is AnimationMode.Full or AnimationMode.Scrub && model.Meta.Animate
            && model.Meta.Narration.Enabled && beats.Count > 0;
        if (narrationActive)
        {
            var (barMarkup, blockH) = NarrationBar(beats, w, bodyH, measurer, hash);
            body.Append(barMarkup);
            bodyH += blockH;
        }

        double totalH = titleH + bodyH;
        string font = options.Font is { } f ? $"'{f.Family}', system-ui, sans-serif" : "'Inter', system-ui, -apple-system, sans-serif";
        string mono = options.Font?.MonoFamily is { } mf ? $"'{mf}', ui-monospace, monospace" : "'IBM Plex Mono', ui-monospace, monospace";
        ThemeMode theme = options.Theme ?? model.Meta.Theme;

        // Animation compiler (§9–10): simulate the flow → CSS keyframes + fx layer.
        // Full loops on load; Scrub drives the same keyframes off scroll position.
        string animCss = "", animDefs = "";
        if (options.Animation is AnimationMode.Full or AnimationMode.Scrub
            && model.Meta.Animate && model.Flow.Steps.Count > 0 && flowEdges.Count > 0)
        {
            Schedule schedule = ScheduleBuilder.Build(model, flowEdges);
            var boxes = new List<NodeBox>(model.Nodes.Count);
            foreach (var n in model.Nodes)
                boxes.Add(layout.Nodes.TryGetValue(n.Id, out var r) ? CardBox(n, r) : default);
            var compiler = new CssCompiler(schedule, hash, boxes, choreo, options.Animation == AnimationMode.Scrub);
            body.Append(compiler.Markup());
            animDefs = compiler.Defs();
            string css = compiler.Css();
            if (!string.IsNullOrEmpty(css)) animCss = "@media (prefers-reduced-motion:no-preference){" + css + "}";
        }

        var svg = new StringBuilder();
        svg.Append($"<svg class=\"beck-svg b-{hash}\" viewBox=\"0 0 {N(w)} {N(totalH)}\" width=\"{N(w)}\" height=\"{N(totalH)}\" ")
           .Append($"style=\"max-width:{N(w)}px;height:auto\" font-family=\"var(--beck-font)\" role=\"img\" aria-label=\"{SvgWriter.Attr(model.Meta.Title ?? "diagram")}\">");
        svg.Append("<style>").Append(Stylesheet.Emit(hash, font, mono, theme)).Append(animCss).Append("</style>");
        svg.Append("<defs>").Append(markers.Defs).Append(extraDefs).Append(animDefs).Append("</defs>");
        svg.Append(TitleBlock(model, w));
        svg.Append($"<g class=\"beck-canvas\" transform=\"translate(0,{N(titleH)})\">").Append(body).Append("</g>");
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static double TitleHeight(DiagramModel model)
    {
        double h = 0;
        if (model.Meta.Title != null) h += 36;      // text-2xl line (32) + mb-1 (4)
        if (model.Meta.Subtitle != null) h += 28;   // 0.9rem line (~20) + mb-2 (8)
        return h;
    }

    private static string TitleBlock(DiagramModel model, double w)
    {
        if (model.Meta.Title is null && model.Meta.Subtitle is null) return "";
        var sb = new StringBuilder("<g class=\"beck-title-block\">");
        double cx = w / 2, y = 0;
        if (model.Meta.Title is { } title)
        {
            y = 22;
            sb.Append($"<text class=\"beck-title\" x=\"{N(cx)}\" y=\"{N(y)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
              .Append($"font-size=\"24\" font-weight=\"700\" letter-spacing=\"-0.02em\">{SvgWriter.Text(title)}</text>");
        }
        if (model.Meta.Subtitle is { } sub)
        {
            double sy = (model.Meta.Title != null ? 36 : 0) + 14;
            sb.Append($"<text class=\"beck-subtitle\" x=\"{N(cx)}\" y=\"{N(sy)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
              .Append($"font-size=\"14.4\">{SvgWriter.Text(sub)}</text>");
        }
        return sb.Append("</g>").ToString();
    }

    /// <summary>
    /// The narration bar (§8.6): one pre-built <c>&lt;g class="beck-beat"&gt;</c> per
    /// caption, stacked at the same spot (opacity 0; the compiler cross-fades them).
    /// Each reserves two lines so the layout never jumps as beats come and go.
    /// </summary>
    private static (string Markup, double BlockH) NarrationBar(
        IReadOnlyList<NarrateStep> beats, double canvasW, double topY, ITextMeasurer m, string hash)
    {
        const double margin = 14.4, padX = 18.4, padY = 9.6, lineHt = 14.72 * 1.45,
                     dotR = 3.5, dotD = 7, gap = 9.6, minContentH = 40.48;
        double barW = Math.Min(736, 0.92 * canvasW);
        double barH = Math.Max(minContentH, 2 * lineHt) + 2 * padY;
        double barTop = topY + margin, cx = canvasW / 2, barLeft = cx - barW / 2, midY = barTop + barH / 2;
        double maxTextW = barW - 2 * padX - dotD - gap;

        var sb = new StringBuilder();
        for (int i = 0; i < beats.Count; i++)
        {
            var lines = WrapNarration(beats[i].Text, maxTextW, m);
            double textW = lines.Max(l => m.Measure(l, FontRole.Narration).Width);
            double left = cx - (dotD + gap + textW) / 2;
            double textCx = left + dotD + gap + textW / 2;
            double firstY = midY - (lines.Count - 1) * lineHt / 2;
            string color = beats[i].Color ?? "var(--beck-text)";

            sb.Append($"<g class=\"beck-beat bbeat{i}-{hash}\" opacity=\"0\" style=\"color:{color}\">");
            sb.Append($"<rect class=\"beck-narration-bar\" x=\"{N(barLeft)}\" y=\"{N(barTop)}\" width=\"{N(barW)}\" height=\"{N(barH)}\" rx=\"12\" ")
              .Append("style=\"fill:color-mix(in srgb,var(--beck-primary) 6%,var(--beck-surface));stroke:color-mix(in srgb,var(--beck-primary) 15%,transparent);filter:drop-shadow(0 4px 12px rgb(0 0 0/.06))\"/>");
            sb.Append($"<circle cx=\"{N(left + dotR)}\" cy=\"{N(midY)}\" r=\"{N(dotR)}\" style=\"fill:currentColor\" opacity=\"0.55\"/>");
            for (int k = 0; k < lines.Count; k++)
                sb.Append($"<text class=\"beck-narration-text\" x=\"{N(textCx)}\" y=\"{N(firstY + k * lineHt)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
                  .Append($"font-size=\"14.72\" font-weight=\"400\" style=\"fill:currentColor\">{SvgWriter.Text(lines[k])}</text>");
            sb.Append("</g>");
        }
        return (sb.ToString(), margin + barH);
    }

    /// <summary>Greedy word-wrap into ≤2 lines at the Narration role (overflow folds into line 2).</summary>
    private static List<string> WrapNarration(string text, double maxW, ITextMeasurer m)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var cur = new StringBuilder();
        foreach (var word in words)
        {
            string cand = cur.Length == 0 ? word : cur + " " + word;
            if (cur.Length > 0 && m.Measure(cand, FontRole.Narration).Width > maxW)
            {
                lines.Add(cur.ToString());
                cur.Clear();
                cur.Append(word);
            }
            else { cur.Clear(); cur.Append(cand); }
        }
        if (cur.Length > 0) lines.Add(cur.ToString());
        if (lines.Count > 2) lines = new List<string> { lines[0], string.Join(" ", lines.Skip(1)) };
        if (lines.Count == 0) lines.Add(text);
        return lines;
    }

    private static string Edge(RoutedEdge e, Markers markers)
    {
        EdgeModel m = e.Edge;
        var sb = new StringBuilder();
        sb.Append($"<path class=\"beck-edge beck-edge--{Tokens.EdgeKind.Wire(m.Kind)}\" d=\"{e.D}\" ")
          .Append($"style=\"stroke:{SvgWriter.Attr(m.Color)}\"");
        if (m.Style == EdgeStyle.Dashed) sb.Append(" stroke-dasharray=\"7 5\"");
        MarkerShape? end = m.MarkerEnd ?? (m.Arrow is ArrowEnds.End or ArrowEnds.Both ? MarkerShape.Arrow : null);
        MarkerShape? start = m.MarkerStart ?? (m.Arrow is ArrowEnds.Start or ArrowEnds.Both ? MarkerShape.Arrow : null);
        if (end is { } es) sb.Append($" marker-end=\"url(#{markers.Ensure(m.Color, es)})\"");
        if (start is { } ss) sb.Append($" marker-start=\"url(#{markers.Ensure(m.Color, ss)})\"");
        sb.Append($" data-edge=\"{SvgWriter.Attr(m.Id)}\"/>");
        return sb.ToString();
    }

    // Text-stack line metrics (must match CardSizer).
    private const double TitleLine = 1.3 * 14, SubLine = 1.35 * 12, TextGap = 3;

    private static string Node(NodeModel node, Rect rect, ITextMeasurer m, string hash, int idx,
        IReadOnlyList<(string Text, string Color)>? statusStates = null)
    {
        var sb = new StringBuilder();
        string accentStyle = $"--beck-accent:{node.Accent}";
        if (node.Surface != null) accentStyle += $";--beck-node-bg:{node.Surface}";
        if (node.TextColor != null) accentStyle += $";--beck-text:{node.TextColor}";
        // A linked node wraps in an SVG <a> so the whole card is clickable; the wrapper
        // sits outside the fx group so effect transforms don't disturb the hit area.
        if (node.Href != null)
        {
            sb.Append($"<a href=\"{SvgWriter.Attr(node.Href)}\"");
            if (node.Target != null) sb.Append($" target=\"{SvgWriter.Attr(node.Target)}\"");
            sb.Append('>');
        }
        // bn{idx} lets the animation compiler target this node's fx wrapper; the inner
        // .beck-fx-node isolates effect transforms (scale/shake) from the positioning
        // translate so pulses/highlights/fails bounce the card in place (§10.2).
        sb.Append($"<g class=\"beck-node-wrap bn{idx}-{hash}\" data-node=\"{SvgWriter.Attr(node.Id)}\" transform=\"translate({N(rect.X)},{N(rect.Y)})\" style=\"{SvgWriter.Attr(accentStyle)}\">");
        sb.Append("<g class=\"beck-fx-node\">");

        double w = rect.W, h = rect.H;
        switch (node.Shape)
        {
            case NodeShape.Pill: EmitPill(sb, node, w, h, m); break;
            case NodeShape.Start: sb.Append($"<circle class=\"beck-node--start\" cx=\"{N(w / 2)}\" cy=\"{N(h / 2)}\" r=\"8\"/>"); break;
            case NodeShape.End:
                sb.Append($"<circle class=\"beck-node--end\" cx=\"{N(w / 2)}\" cy=\"{N(h / 2)}\" r=\"7\"/>")
                  .Append($"<circle class=\"beck-end-dot\" cx=\"{N(w / 2)}\" cy=\"{N(h / 2)}\" r=\"3.5\"/>");
                break;
            case NodeShape.Class: EmitClass(sb, node, w, h, m, hash); break;
            default:
                if (node.Variant == NodeVariant.Ghost || node.Kind == NodeKind.Ghost) EmitGhost(sb, node, w, h, m);
                else EmitCard(sb, node, w, h, m, idx, hash, statusStates);
                break;
        }

        sb.Append("</g></g>");
        if (node.Href != null) sb.Append("</a>");
        return sb.ToString();
    }

    /// <summary>The node's card box in canvas coords + corner radius, for effect overlays.</summary>
    private static NodeBox CardBox(NodeModel node, Rect r)
    {
        double rx = node.Shape switch
        {
            NodeShape.Pill => r.H / 2,
            NodeShape.Class => 12,
            NodeShape.Start or NodeShape.End => Math.Min(r.W, r.H) / 2,
            _ => 14,
        };
        return new NodeBox(r.X + 0.75, r.Y + 0.75, r.W - 1.5, r.H - 1.5, rx);
    }

    /// <summary>A status pill — chip bg tinted 14% of the colour + coloured text — at (x, sy).</summary>
    private static void StatusPill(StringBuilder sb, string text, string color, double x, double sy, double h, ITextMeasurer m)
    {
        double sw = m.Measure(text, FontRole.Status).Width;
        sb.Append($"<rect x=\"{N(x)}\" y=\"{N(sy)}\" width=\"{N(sw + 16)}\" height=\"{N(h)}\" rx=\"{N(h / 2)}\" style=\"fill:color-mix(in srgb,{color} 14%,transparent)\"/>");
        sb.Append($"<text x=\"{N(x + 8)}\" y=\"{N(sy + h / 2)}\" font-size=\"10.4\" font-weight=\"500\" dominant-baseline=\"central\" text-anchor=\"start\" textLength=\"{N(sw)}\" lengthAdjust=\"spacingAndGlyphs\" style=\"fill:{color}\">{SvgWriter.Text(text)}</text>");
    }

    private static void EmitPill(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m)
    {
        sb.Append($"<rect class=\"beck-node beck-node--pill\" x=\"0.75\" y=\"0.75\" width=\"{N(w - 1.5)}\" height=\"{N(h - 1.5)}\" rx=\"{N(h / 2)}\"/>");
        const double titleLine = 1.3 * 14, subLine = 1.3 * 10.88, gap = 1;
        double stackH = titleLine + (node.Subtitle != null ? gap + subLine : 0);
        double top = h / 2 - stackH / 2;
        CenterLine(sb, node.Title, "beck-node-title", w / 2, top + titleLine / 2, 14, 600, m, FontRole.PillTitle);
        if (node.Subtitle is { } sub)
            CenterLine(sb, sub, "beck-node-subtitle", w / 2, top + titleLine + gap + subLine / 2, 10.88, 400, m, FontRole.PillSubtitle);
    }

    private static void EmitClass(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash)
    {
        string clip = $"cc-{SvgWriter.Attr(node.Id)}-{hash}";
        sb.Append($"<clipPath id=\"{clip}\"><rect x=\"0\" y=\"0\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"12\"/></clipPath>");
        sb.Append($"<rect class=\"beck-node beck-node--class\" x=\"0.75\" y=\"0.75\" width=\"{N(w - 1.5)}\" height=\"{N(h - 1.5)}\" rx=\"12\"/>");
        sb.Append($"<g clip-path=\"url(#{clip})\">");

        const double stereoLine = 1.3 * 10.4, titleLine = 1.4 * 14, headPadY = 8, memberLine = 1.45 * 11.52, sectionPadY = 7, memberGap = 2;
        bool hasStereo = node.Stereotype != null;
        double headH = (hasStereo ? stereoLine : 0) + titleLine + headPadY * 2 + 1;
        sb.Append($"<rect class=\"beck-class-head\" x=\"0\" y=\"0\" width=\"{N(w)}\" height=\"{N(headH)}\"/>");
        double ty = headPadY;
        if (hasStereo)
        {
            CenterLine(sb, $"«{node.Stereotype}»", "beck-class-stereo", w / 2, ty + stereoLine / 2, 10.4, 400, m, FontRole.ClassStereotype);
            ty += stereoLine;
        }
        CenterLine(sb, node.Title, "beck-class-title", w / 2, ty + titleLine / 2, 14, 600, m, FontRole.ClassTitle);
        sb.Append($"<line class=\"beck-class-head-border\" x1=\"0\" y1=\"{N(headH)}\" x2=\"{N(w)}\" y2=\"{N(headH)}\"/>");

        double y = headH;
        bool prior = false;
        foreach (var (members, css) in new[] { (node.Fields, "beck-class-field"), (node.Methods, "beck-class-method") })
        {
            if (members.Count == 0) continue;
            if (prior) { sb.Append($"<line class=\"beck-class-divider\" x1=\"0\" y1=\"{N(y)}\" x2=\"{N(w)}\" y2=\"{N(y)}\"/>"); y += 1; }
            double my = y + sectionPadY;
            foreach (var member in members)
            {
                sb.Append($"<text class=\"{css}\" x=\"14\" y=\"{N(my + memberLine / 2)}\" font-size=\"11.52\" font-weight=\"400\" ")
                  .Append($"dominant-baseline=\"central\" text-anchor=\"start\" style=\"font-family:var(--beck-font-mono)\">{SvgWriter.Text(member)}</text>");
                my += memberLine + memberGap;
            }
            y += members.Count * memberLine + (members.Count - 1) * memberGap + sectionPadY * 2;
            prior = true;
        }
        sb.Append("</g>");
    }

    private static void CenterLine(StringBuilder sb, string text, string cls, double cx, double cy, double size, int weight, ITextMeasurer m, FontRole role)
    {
        double tl = m.Measure(text, role).Width;
        sb.Append($"<text class=\"{cls}\" x=\"{N(cx)}\" y=\"{N(cy)}\" font-size=\"{N(size)}\" font-weight=\"{weight}\" ")
          .Append($"dominant-baseline=\"central\" text-anchor=\"middle\" textLength=\"{N(tl)}\" lengthAdjust=\"spacingAndGlyphs\">")
          .Append(SvgWriter.Text(text)).Append("</text>");
    }

    private static void EmitCard(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m,
        int idx, string hash, IReadOnlyList<(string Text, string Color)>? states)
    {
        string cls = "beck-node";
        if (node.Kind == NodeKind.External) cls += " beck-node--external";
        if (node.Variant == NodeVariant.Subtle) cls += " beck-node--subtle";
        sb.Append($"<rect class=\"{cls}\" x=\"0.75\" y=\"0.75\" width=\"{N(w - 1.5)}\" height=\"{N(h - 1.5)}\" rx=\"14\"/>");

        bool hasIcon = Icons.ResolveIcon(node.Icon) != null;
        double textX = 16 + (hasIcon ? 34 + 12 : 0);
        if (hasIcon)
        {
            double chipY = h / 2 - 17;
            sb.Append($"<rect class=\"beck-icon-chip\" x=\"16\" y=\"{N(chipY)}\" width=\"34\" height=\"34\" rx=\"9\"/>");
            sb.Append(IconSvg(node.Icon!, 16 + 7, chipY + 7, 20));
        }

        const double statusChipH = 3 * 2 + 1.2 * 10.4; // 18.48
        // Wrap the title/subtitle into the SAME lines CardSizer measured the box for, so the drawn
        // text stays inside it (a single overflowing <text> was the "text escapes its box" bug).
        double avail = CardSizer.CardTextAvail(node, m);
        var titleLines = CardSizer.WrapText(m, node.Title, FontRole.CardTitle, avail);
        var subLines = node.Subtitle != null ? CardSizer.WrapText(m, node.Subtitle, FontRole.CardSubtitle, avail) : null;
        double textColH = titleLines.Count * TitleLine
            + (subLines != null ? TextGap + subLines.Count * SubLine : 0)
            + (node.Status != null ? TextGap + 2 + statusChipH : 0);
        double top = h / 2 - textColH / 2;
        double stackY = top;
        foreach (string line in titleLines)
        {
            Line(sb, line, "beck-node-title", textX, stackY + TitleLine / 2, 14, 600, m, FontRole.CardTitle);
            stackY += TitleLine;
        }
        if (subLines != null)
        {
            stackY += TextGap;
            foreach (string line in subLines)
            {
                Line(sb, line, "beck-node-subtitle", textX, stackY + SubLine / 2, 12, 400, m, FontRole.CardSubtitle);
                stackY += SubLine;
            }
        }
        bool hasStates = states is { Count: > 1 };
        if (node.Status is { } || hasStates)
        {
            double sy = stackY + TextGap + 2;
            if (hasStates)
            {
                // The flow swaps this pill: pre-build one group per (text,color) state,
                // state 0 (resting, possibly empty) visible, the rest hidden — the
                // compiler cross-fades. A status-less target overhangs (accepted, §15).
                for (int si = 0; si < states!.Count; si++)
                {
                    sb.Append($"<g class=\"beck-status-state bss{idx}-{si}-{hash}\"{(si == 0 ? "" : " opacity=\"0\"")}>");
                    if (states[si].Text.Length > 0) StatusPill(sb, states[si].Text, states[si].Color, textX, sy, statusChipH, m);
                    sb.Append("</g>");
                }
            }
            else
            {
                string status = node.Status!;
                double sw = m.Measure(status, FontRole.Status).Width;
                sb.Append($"<rect class=\"beck-status-bg\" x=\"{N(textX)}\" y=\"{N(sy)}\" width=\"{N(sw + 16)}\" height=\"{N(statusChipH)}\" rx=\"{N(statusChipH / 2)}\"/>");
                sb.Append($"<text class=\"beck-status-text\" x=\"{N(textX + 8)}\" y=\"{N(sy + statusChipH / 2)}\" font-size=\"10.4\" font-weight=\"500\" dominant-baseline=\"central\" text-anchor=\"start\" textLength=\"{N(sw)}\" lengthAdjust=\"spacingAndGlyphs\">{SvgWriter.Text(status)}</text>");
            }
        }
    }

    private static void EmitGhost(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m)
    {
        sb.Append($"<rect class=\"beck-node beck-node--ghost\" x=\"0.75\" y=\"0.75\" width=\"{N(w - 1.5)}\" height=\"{N(h - 1.5)}\" rx=\"16\"/>");
        bool hasIcon = Icons.ResolveIcon(node.Icon) != null;
        double rowH = Math.Max(hasIcon ? 16 : 0, 1.4 * 11.52);
        double rowTop = h / 2 - (node.Status != null ? (rowH + TextGap + 1.4 * 9.92) / 2 : rowH / 2);
        double labelX = 14 + (hasIcon ? 16 + 7 : 0);
        if (hasIcon) sb.Append(IconSvg(node.Icon!, 14, rowTop + rowH / 2 - 7, 14));
        Line(sb, node.Title, "beck-ghost-label", labelX, rowTop + rowH / 2, 11.52, 500, m, FontRole.GhostLabel);
    }

    private static void Line(StringBuilder sb, string text, string cls, double x, double cy, double size, int weight, ITextMeasurer m, FontRole role)
    {
        double tl = m.Measure(text, role).Width;
        sb.Append($"<text class=\"{cls}\" x=\"{N(x)}\" y=\"{N(cy)}\" font-size=\"{N(size)}\" font-weight=\"{weight}\" ")
          .Append($"dominant-baseline=\"central\" text-anchor=\"start\" textLength=\"{N(tl)}\" lengthAdjust=\"spacingAndGlyphs\">")
          .Append(SvgWriter.Text(text)).Append("</text>");
    }

    private static string IconSvg(string icon, double x, double y, double size)
    {
        string? body = Icons.ResolveIcon(icon);
        if (body is null) return "";
        // Position the resolved <svg ...> and tint it via the accent (currentColor).
        return body.Replace("<svg ",
            $"<svg class=\"beck-icon\" x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(size)}\" height=\"{N(size)}\" style=\"color:var(--beck-accent)\" ", StringComparison.Ordinal);
    }
}
