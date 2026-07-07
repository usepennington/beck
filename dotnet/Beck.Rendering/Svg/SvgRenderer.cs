using System.Text;
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
        // measure → layout → route
        var sizes = model.Nodes.ToDictionary(n => n.Id, n => CardSizer.Measure(n, measurer));
        LayoutResult layout = LayeredLayout.Compute(model, sizes);
        var edges = EdgePainter.RouteEdges(model, layout);

        var markers = new Markers(hash);
        double titleH = TitleHeight(model);
        double w = layout.Width;
        double totalH = titleH + layout.Height;

        // font tokens: the measured font (options.Font) or the Inter default.
        string font = options.Font is { } f ? $"'{f.Family}', system-ui, sans-serif" : "'Inter', system-ui, -apple-system, sans-serif";
        string mono = options.Font?.MonoFamily is { } mf ? $"'{mf}', ui-monospace, monospace" : "'IBM Plex Mono', ui-monospace, monospace";
        ThemeMode theme = options.Theme ?? model.Meta.Theme;

        var body = new StringBuilder();

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

        // z2 edges (+ markers)
        body.Append("<g class=\"beck-overlay\">");
        foreach (var e in edges) body.Append(Edge(e, markers));
        body.Append("</g>");

        // z3 nodes
        body.Append("<g class=\"beck-nodes\">");
        foreach (var n in model.Nodes)
            if (layout.Nodes.TryGetValue(n.Id, out var r)) body.Append(Node(n, r, measurer));
        body.Append("</g>");

        // z4 group labels
        body.Append("<g class=\"beck-group-labels\">");
        foreach (var g in model.Groups.Where(g => layout.Groups.ContainsKey(g.Id) && !string.IsNullOrEmpty(g.Label)))
        {
            Rect r = layout.Groups[g.Id];
            string label = g.Label.ToUpperInvariant();
            double lw = measurer.Measure(g.Label, FontRole.GroupLabel).Width;
            double lx = r.X + 14, ly = r.Y - 9;
            body.Append($"<rect class=\"beck-group-label-bg\" x=\"{N(lx - 5.6)}\" y=\"{N(ly - 8)}\" width=\"{N(lw + 11.2)}\" height=\"16\" rx=\"3\"/>");
            body.Append($"<text class=\"beck-group-label\" x=\"{N(lx)}\" y=\"{N(ly)}\" dominant-baseline=\"central\" ")
                .Append($"font-size=\"11.2\" font-weight=\"600\" letter-spacing=\"0.04em\" style=\"fill:{SvgWriter.Attr(g.Accent)}\">{SvgWriter.Text(label)}</text>");
        }
        body.Append("</g>");

        var svg = new StringBuilder();
        svg.Append($"<svg class=\"beck-svg b-{hash}\" viewBox=\"0 0 {N(w)} {N(totalH)}\" width=\"{N(w)}\" height=\"{N(totalH)}\" ")
           .Append($"style=\"max-width:{N(w)}px;height:auto\" font-family=\"var(--beck-font)\" role=\"img\" aria-label=\"{SvgWriter.Attr(model.Meta.Title ?? "diagram")}\">");
        svg.Append("<style>").Append(Stylesheet.Emit(hash, font, mono, theme)).Append("</style>");
        svg.Append("<defs>").Append(markers.Defs).Append("</defs>");
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

    private static string Node(NodeModel node, Rect rect, ITextMeasurer m)
    {
        var sb = new StringBuilder();
        string accentStyle = $"--beck-accent:{node.Accent}";
        if (node.Surface != null) accentStyle += $";--beck-node-bg:{node.Surface}";
        if (node.TextColor != null) accentStyle += $";--beck-text:{node.TextColor}";
        sb.Append($"<g class=\"beck-node-wrap\" data-node=\"{SvgWriter.Attr(node.Id)}\" transform=\"translate({N(rect.X)},{N(rect.Y)})\" style=\"{SvgWriter.Attr(accentStyle)}\">");

        bool ghost = node.Variant == NodeVariant.Ghost || node.Kind == NodeKind.Ghost;
        double w = rect.W, h = rect.H;

        if (ghost) EmitGhost(sb, node, w, h, m);
        else EmitCard(sb, node, w, h, m);

        sb.Append("</g>");
        return sb.ToString();
    }

    private static void EmitCard(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m)
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

        double textColH = TitleLine
            + (node.Subtitle != null ? TextGap + SubLine : 0);
        double top = h / 2 - textColH / 2;
        Line(sb, node.Title, "beck-node-title", textX, top + TitleLine / 2, 14, 600, m, FontRole.CardTitle);
        if (node.Subtitle is { } sub)
            Line(sb, sub, "beck-node-subtitle", textX, top + TitleLine + TextGap + SubLine / 2, 12, 400, m, FontRole.CardSubtitle);
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
