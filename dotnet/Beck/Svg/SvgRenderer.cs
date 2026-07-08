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

    /// <summary>Format a style integer (mix percentage, font weight) invariantly, so a comma-decimal
    /// locale can never perturb the emitted CSS/SVG.</summary>
    private static string P(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The <c>textLength</c>/<c>lengthAdjust</c> guard attributes for a measured run, or
    /// empty when the active <see cref="TextLengthGuard"/> mode suppresses them. Prefixed with a
    /// space so it drops straight into an attribute list.</summary>
    internal static string Guard(double w, bool emit) =>
        emit ? $" textLength=\"{N(w)}\" lengthAdjust=\"spacingAndGlyphs\"" : "";

    /// <summary>The rendered form of a measured run: uppercased when the style's role spec is
    /// uppercase, so the drawn text matches the (uppercase-measured) box and its <c>textLength</c>.
    /// Classic has no uppercase role on these emit paths, so this is a no-op there — byte-identical.
    /// The always-uppercase group/band labels keep their own bespoke <c>ToUpperInvariant()</c>.</summary>
    private static string Cased(FontRoleSpec spec, string text) => spec.Uppercase ? text.ToUpperInvariant() : text;

    public static string Render(DiagramModel model, ITextMeasurer measurer, string hash, SvgRenderOptions options, BeckStyle style)
    {
        StyleGeometry geo = style.Geometry;
        // The per-text textLength guard emission mode. All → always (default, byte-identical);
        // Off → never; FallbackOnly → only when the measurer approximates (embedded metrics table).
        bool guard = options.TextLengthGuard switch
        {
            TextLengthGuard.Off => false,
            TextLengthGuard.FallbackOnly => measurer.IsApproximate,
            _ => true,
        };
        var sizes = model.Nodes.ToDictionary(n => n.Id, n => CardSizer.Measure(n, measurer, geo, style.Typography.Roles, style.Typography.TitlePrefix, style.Typography.TitleSuffix));
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
            var painter = new SequencePainter(hash, markers, measurer, style);
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
                if (layout.Nodes.TryGetValue(model.Nodes[i].Id, out var r)) body.Append(Node(model.Nodes[i], r, measurer, hash, i, style, guard, StatesFor(model.Nodes[i].Id)));
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
                body.Append(Artwork.Rect(style, "beck-group", r.X, r.Y, r.W, r.H, geo.GroupRadius, hash + ":" + g.Id,
                    $"stroke:color-mix(in srgb, {SvgWriter.Attr(g.Accent)} {P(style.Mix.GroupBorder)}%, transparent)"));
                // Blueprint's technical-drawing dimension line along the group's top edge (no-op for
                // every other style / a zero DimensionTick — byte-identical).
                body.Append(Artwork.GroupDimension(style, r.X, r.Y, r.W));
            }
            body.Append("</g>");

            // z2 edges (+ markers), then labels (pass 2 — mid labels dodge every line)
            body.Append("<g class=\"beck-overlay\">");
            foreach (var e in edges) body.Append(Edge(e, markers, style, hash));
            var placer = new LabelPlacer(layout.Nodes.Values, layout.Width, layout.Height, style, guard);
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
                if (layout.Nodes.TryGetValue(model.Nodes[i].Id, out var r)) body.Append(Node(model.Nodes[i], r, measurer, hash, i, style, guard, StatesFor(model.Nodes[i].Id)));
            body.Append("</g>");

            // z4 group labels
            body.Append("<g class=\"beck-group-labels\">");
            foreach (var g in model.Groups.Where(g => layout.Groups.ContainsKey(g.Id) && !string.IsNullOrEmpty(g.Label)))
            {
                Rect r = layout.Groups[g.Id];
                FontRoleSpec glSpec = style.Typography.Roles.Of(FontRole.GroupLabel);
                double lw = measurer.Measure(g.Label, FontRole.GroupLabel, glSpec).Width;
                double lx = r.X + 14, ly = r.Y;
                body.Append($"<rect class=\"beck-group-label-bg\" x=\"{N(lx - 5.6)}\" y=\"{N(ly - 8)}\" width=\"{N(lw + 11.2)}\" height=\"16\" rx=\"{N(geo.GroupLabelBgRadius)}\"/>");
                body.Append($"<text class=\"beck-group-label\" x=\"{N(lx)}\" y=\"{N(ly)}\" dominant-baseline=\"central\" ")
                    .Append($"font-size=\"{N(glSpec.SizePx)}\" font-weight=\"{P(glSpec.Weight)}\" letter-spacing=\"{SvgWriter.Ls(glSpec.LetterSpacingEm)}\" style=\"fill:{SvgWriter.Attr(g.Accent)}\">{SvgWriter.Text(g.Label.ToUpperInvariant())}</text>");
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
            var (barMarkup, blockH) = NarrationBar(beats, w, bodyH, measurer, hash, style);
            body.Append(barMarkup);
            bodyH += blockH;
        }

        double totalH = titleH + bodyH;
        string font = options.Font is { } f ? $"'{f.Family}', system-ui, sans-serif" : style.Typography.SansFamily;
        string mono = options.Font?.MonoFamily is { } mf ? $"'{mf}', ui-monospace, monospace" : style.Typography.MonoFamily;
        ThemeMode theme = options.Theme ?? model.Meta.Theme;

        // Animation compiler (§9–10): simulate the flow → CSS keyframes + fx layer.
        // Full loops on load; Scrub drives the same keyframes off scroll position.
        string animCss = "", animDefs = "";
        if (options.Animation is AnimationMode.Full or AnimationMode.Scrub
            && model.Meta.Animate && model.Flow.Steps.Count > 0 && flowEdges.Count > 0)
        {
            Schedule schedule = ScheduleBuilder.Build(model, flowEdges, style.Motion);
            var boxes = new List<NodeBox>(model.Nodes.Count);
            foreach (var n in model.Nodes)
                boxes.Add(layout.Nodes.TryGetValue(n.Id, out var r) ? CardBox(n, r, geo) : default);
            var compiler = new CssCompiler(schedule, hash, boxes, style.Motion, style.Strokes, choreo, options.Animation == AnimationMode.Scrub);
            body.Append(compiler.Markup());
            animDefs = compiler.Defs();
            string css = compiler.Css();
            if (!string.IsNullOrEmpty(css)) animCss = "@media (prefers-reduced-motion:no-preference){" + css + "}";
        }

        var svg = new StringBuilder();
        svg.Append($"<svg class=\"beck-svg b-{hash}\" viewBox=\"0 0 {N(w)} {N(totalH)}\" width=\"{N(w)}\" height=\"{N(totalH)}\" ")
           .Append($"style=\"max-width:{N(w)}px;height:auto\" font-family=\"var(--beck-font)\" role=\"img\" aria-label=\"{SvgWriter.Attr(model.Meta.Title ?? "diagram")}\">");
        svg.Append("<style>").Append(Stylesheet.Emit(hash, font, mono, theme, style)).Append(animCss).Append("</style>");
        svg.Append("<defs>").Append(markers.Defs).Append(extraDefs).Append(animDefs).Append(Stylesheet.StyleDefs(hash, style)).Append("</defs>");
        svg.Append(TitleBlock(model, w, style));
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

    private static string TitleBlock(DiagramModel model, double w, BeckStyle style)
    {
        if (model.Meta.Title is null && model.Meta.Subtitle is null) return "";
        var sb = new StringBuilder("<g class=\"beck-title-block\">");
        double cx = w / 2, y = 0;
        if (model.Meta.Title is { } title)
        {
            FontRoleSpec spec = style.Typography.Roles.Of(FontRole.DiagramTitle);
            y = 22;
            sb.Append($"<text class=\"beck-title\" x=\"{N(cx)}\" y=\"{N(y)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
              .Append($"font-size=\"{N(spec.SizePx)}\" font-weight=\"{P(spec.Weight)}\" letter-spacing=\"{SvgWriter.Ls(spec.LetterSpacingEm)}\">{SvgWriter.Text(title)}</text>");
        }
        if (model.Meta.Subtitle is { } sub)
        {
            FontRoleSpec spec = style.Typography.Roles.Of(FontRole.DiagramSubtitle);
            double sy = (model.Meta.Title != null ? 36 : 0) + 14;
            sb.Append($"<text class=\"beck-subtitle\" x=\"{N(cx)}\" y=\"{N(sy)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
              .Append($"font-size=\"{N(spec.SizePx)}\">{SvgWriter.Text(sub)}</text>");
        }
        return sb.Append("</g>").ToString();
    }

    /// <summary>
    /// The narration bar (§8.6): one pre-built <c>&lt;g class="beck-beat"&gt;</c> per
    /// caption, stacked at the same spot (opacity 0; the compiler cross-fades them).
    /// Each reserves two lines so the layout never jumps as beats come and go.
    /// </summary>
    private static (string Markup, double BlockH) NarrationBar(
        IReadOnlyList<NarrateStep> beats, double canvasW, double topY, ITextMeasurer m, string hash, BeckStyle style)
    {
        const double margin = 14.4, padX = 18.4, padY = 9.6, lineHt = 14.72 * 1.45,
                     dotR = 3.5, dotD = 7, minContentH = 40.48;
        // The bullet→text gap is a style field (terminal widens it for its mono caption); classic = 9.6.
        double gap = style.Geometry.NarrationBulletGap;
        double barW = Math.Min(736, 0.92 * canvasW);
        double barH = Math.Max(minContentH, 2 * lineHt) + 2 * padY;
        double barTop = topY + margin, cx = canvasW / 2, barLeft = cx - barW / 2, midY = barTop + barH / 2;
        double maxTextW = barW - 2 * padX - dotD - gap;

        var sb = new StringBuilder();
        FontRoleSpec nSpec = style.Typography.Roles.Of(FontRole.Narration);
        bool figCaption = style.Typography.NarrationFigureCaption;
        // Editorial renders the caption as a numbered figure caption: a deterministic "Fig. N — "
        // prefix (N = 1-based beat order, content-derived + stable) and serif-italic text. The prefix
        // is prepended before wrapping/measuring so the measured string == the rendered string.
        string textStyle = figCaption ? "fill:currentColor;font-style:italic" : "fill:currentColor";
        for (int i = 0; i < beats.Count; i++)
        {
            string beatText = figCaption
                ? $"Fig. {(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)} — {beats[i].Text}"
                : beats[i].Text;
            var lines = WrapNarration(beatText, maxTextW, m, nSpec);
            double textW = lines.Max(l => m.Measure(l, FontRole.Narration, nSpec).Width);
            double left = cx - (dotD + gap + textW) / 2;
            double textCx = left + dotD + gap + textW / 2;
            double firstY = midY - (lines.Count - 1) * lineHt / 2;
            string color = beats[i].Color ?? "var(--beck-text)";

            sb.Append($"<g class=\"beck-beat bbeat{i}-{hash}\" opacity=\"0\" style=\"color:{color}\">");
            if (figCaption)
                // Editorial's figure caption: no filled/bordered bar (a solid surface-filled box reads
                // as a black block on a dark page, at odds with the serif "Fig. N —" identity). Instead a
                // transparent surface with a single hairline rule above the caption — the print-figure
                // separator that matches the sequence caption. The bullet dot + serif text carry the rest.
                sb.Append($"<line class=\"beck-narration-rule\" x1=\"{N(barLeft)}\" y1=\"{N(barTop)}\" x2=\"{N(barLeft + barW)}\" y2=\"{N(barTop)}\" ")
                  .Append($"style=\"stroke:color-mix(in srgb,var(--beck-primary) {P(style.Mix.NarrationBorder)}%,var(--beck-text-faint));stroke-width:{N(style.Geometry.HairlineStroke)}\"/>");
            else
                sb.Append($"<rect class=\"beck-narration-bar\" x=\"{N(barLeft)}\" y=\"{N(barTop)}\" width=\"{N(barW)}\" height=\"{N(barH)}\" rx=\"{N(style.Geometry.NarrationRadius)}\" ")
                  .Append($"style=\"fill:color-mix(in srgb,var(--beck-primary) {P(style.Mix.NarrationFill)}%,var(--beck-surface));stroke:color-mix(in srgb,var(--beck-primary) {P(style.Mix.NarrationBorder)}%,transparent);filter:{style.Geometry.NarrationShadow}\"/>");
            sb.Append($"<circle cx=\"{N(left + dotR)}\" cy=\"{N(midY)}\" r=\"{N(dotR)}\" style=\"fill:currentColor\" opacity=\"0.55\"/>");
            for (int k = 0; k < lines.Count; k++)
                sb.Append($"<text class=\"beck-narration-text\" x=\"{N(textCx)}\" y=\"{N(firstY + k * lineHt)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
                  .Append($"font-size=\"{N(nSpec.SizePx)}\" font-weight=\"{P(nSpec.Weight)}\" style=\"{textStyle}\">{SvgWriter.Text(lines[k])}</text>");
            sb.Append("</g>");
        }
        return (sb.ToString(), margin + barH);
    }

    /// <summary>Greedy word-wrap into ≤2 lines at the Narration role (overflow folds into line 2).</summary>
    private static List<string> WrapNarration(string text, double maxW, ITextMeasurer m, FontRoleSpec nSpec)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var cur = new StringBuilder();
        foreach (var word in words)
        {
            string cand = cur.Length == 0 ? word : cur + " " + word;
            if (cur.Length > 0 && m.Measure(cand, FontRole.Narration, nSpec).Width > maxW)
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

    private static string Edge(RoutedEdge e, Markers markers, BeckStyle style, string hash)
    {
        EdgeModel m = e.Edge;
        var sb = new StringBuilder();
        // Glow's luminous edges: an edge that uses the *default* colour (var(--beck-edge)) paints with
        // the style's single hash-scoped gradient defined in <defs>; author-coloured edges keep their
        // explicit colour, and the arrow markers (still edge-coloured) match the gradient's endpoints.
        string stroke = style.Strokes.GradientEdges && m.Color == "var(--beck-edge)"
            ? $"url(#beck-edge-grad-{hash})"
            : SvgWriter.Attr(m.Color);
        sb.Append($"<path class=\"beck-edge beck-edge--{Tokens.EdgeKind.Wire(m.Kind)}\" d=\"{e.D}\" ")
          .Append($"style=\"stroke:{stroke}\"");
        if (m.Style == EdgeStyle.Dashed) sb.Append($" stroke-dasharray=\"{style.Strokes.EdgeDash}\"");
        MarkerShape? end = m.MarkerEnd ?? (m.Arrow is ArrowEnds.End or ArrowEnds.Both ? MarkerShape.Arrow : null);
        MarkerShape? start = m.MarkerStart ?? (m.Arrow is ArrowEnds.Start or ArrowEnds.Both ? MarkerShape.Arrow : null);
        if (end is { } es) sb.Append($" marker-end=\"url(#{markers.Ensure(m.Color, es)})\"");
        if (start is { } ss) sb.Append($" marker-start=\"url(#{markers.Ensure(m.Color, ss)})\"");
        sb.Append($" data-edge=\"{SvgWriter.Attr(m.Id)}\"/>");
        // Circuit's via dots: a small circle at every genuine bend of the edge's already-computed route
        // polyline (the elbow where a right-angle trace turns). Read straight off the existing route
        // geometry (e.Points) — the router is untouched and the edge stays one continuous <path> above;
        // the vias are additional sibling elements drawn on top of the trace. Deterministic (geometry
        // only, no RNG). Every other style emits nothing here.
        if (style.Artwork == StyleArtwork.Circuit)
            foreach (Point b in Bends(e.Points))
                sb.Append($"<circle class=\"beck-via\" cx=\"{N(b.X)}\" cy=\"{N(b.Y)}\" r=\"{N(style.Geometry.ViaRadius)}\" style=\"fill:var(--beck-via, var(--beck-edge))\"/>");
        // Metro's station dots: a white-filled, edge-coloured-ring circle at each of the edge's two
        // anchor endpoints (the route polyline's first + last point), drawn over the thick line. Read
        // from the existing route geometry — router untouched, edge still one continuous <path> above.
        if (style.Artwork == StyleArtwork.Metro && e.Points.Count > 0)
        {
            sb.Append(Artwork.Station(style, e.Points[0].X, e.Points[0].Y, m.Color));
            sb.Append(Artwork.Station(style, e.Points[^1].X, e.Points[^1].Y, m.Color));
        }
        return sb.ToString();
    }

    /// <summary>The genuine bends (interior direction-changes) of a route polyline — the elbows where a
    /// step-round trace turns. Replicates the router's own dedupe (drop near-duplicate vertices) and
    /// collinearity test (a straight-through vertex is not a corner) so the returned points line up one-
    /// for-one with the <c>Q</c> corners in the emitted path <c>d</c>. Pure read of route geometry — no
    /// mutation of <c>Route/</c>.</summary>
    private static IEnumerable<Point> Bends(IReadOnlyList<Point> pts)
    {
        var dedup = new List<Point>();
        foreach (Point p in pts)
        {
            if (dedup.Count == 0) { dedup.Add(p); continue; }
            Point prev = dedup[^1];
            if (Math.Abs(prev.X - p.X) > 0.5 || Math.Abs(prev.Y - p.Y) > 0.5) dedup.Add(p);
        }
        for (int i = 1; i < dedup.Count - 1; i++)
        {
            Point a = dedup[i - 1], c = dedup[i], d = dedup[i + 1];
            double cross = (c.X - a.X) * (d.Y - a.Y) - (c.Y - a.Y) * (d.X - a.X);
            if (Math.Abs(cross) >= 0.01) yield return c;
        }
    }

    private static string Node(NodeModel node, Rect rect, ITextMeasurer m, string hash, int idx, BeckStyle style,
        bool guard, IReadOnlyList<(string Text, string Color)>? statusStates = null)
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
            case NodeShape.Pill: EmitPill(sb, node, w, h, m, hash, style, guard); break;
            case NodeShape.Start: sb.Append(Artwork.Circle(style, "beck-node--start", w / 2, h / 2, 8, hash + ":" + node.Id)); break;
            case NodeShape.End:
                sb.Append(Artwork.Circle(style, "beck-node--end", w / 2, h / 2, 7, hash + ":" + node.Id))
                  .Append(Artwork.Circle(style, "beck-end-dot", w / 2, h / 2, 3.5, hash + ":" + node.Id + ":dot"));
                break;
            case NodeShape.Class: EmitClass(sb, node, w, h, m, hash, style, guard); break;
            default:
                if (node.Variant == NodeVariant.Ghost || node.Kind == NodeKind.Ghost) EmitGhost(sb, node, w, h, m, hash, style, guard);
                else EmitCard(sb, node, w, h, m, idx, hash, style, guard, statusStates);
                break;
        }

        sb.Append("</g></g>");
        if (node.Href != null) sb.Append("</a>");
        return sb.ToString();
    }

    /// <summary>The node's card box in canvas coords + corner radius, for effect overlays.</summary>
    /// <remarks>Note the intentional quirk (preserved for byte-identity): there is no ghost branch,
    /// so ghost nodes fall to <see cref="StyleGeometry.CardRadius"/> (14) here even though their rect
    /// renders at <see cref="StyleGeometry.GhostRadius"/> (16).</remarks>
    private static NodeBox CardBox(NodeModel node, Rect r, StyleGeometry geo)
    {
        double rx = node.Shape switch
        {
            NodeShape.Pill => r.H / 2,
            NodeShape.Class => geo.ClassRadius,
            NodeShape.Start or NodeShape.End => Math.Min(r.W, r.H) / 2,
            _ => geo.CardRadius,
        };
        double inset = geo.NodeBorderInset;
        return new NodeBox(r.X + inset, r.Y + inset, r.W - 2 * inset, r.H - 2 * inset, rx);
    }

    /// <summary>A status pill — chip bg tinted by the status-pill ratio + coloured text — at (x, sy).</summary>
    private static void StatusPill(StringBuilder sb, string text, string color, double x, double sy, double h, ITextMeasurer m, BeckStyle style, bool guard)
    {
        FontRoleSpec spec = style.Typography.Roles.Of(FontRole.Status);
        double sw = m.Measure(text, FontRole.Status, spec).Width;
        string shown = Cased(spec, text);
        sb.Append($"<rect x=\"{N(x)}\" y=\"{N(sy)}\" width=\"{N(sw + 16)}\" height=\"{N(h)}\" rx=\"{N(h / 2)}\" style=\"fill:color-mix(in srgb,{color} {P(style.Mix.StatusPill)}%,transparent)\"/>");
        sb.Append($"<text x=\"{N(x + 8)}\" y=\"{N(sy + h / 2)}\" font-size=\"{N(spec.SizePx)}\" font-weight=\"{P(spec.Weight)}\" dominant-baseline=\"central\" text-anchor=\"start\"{Guard(sw, guard)} style=\"fill:{color}\">{SvgWriter.Text(shown)}</text>");
    }

    private static void EmitPill(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard)
    {
        StyleGeometry geo = style.Geometry;
        double bi = geo.NodeBorderInset;
        sb.Append(Artwork.Rect(style, "beck-node beck-node--pill", bi, bi, w - 2 * bi, h - 2 * bi, h / 2, hash + ":" + node.Id, shadow: true));
        double titleLine = geo.CardTitleLine, subLine = geo.PillSubLine, gap = geo.PillGap;
        double stackH = titleLine + (node.Subtitle != null ? gap + subLine : 0);
        double top = h / 2 - stackH / 2;
        CenterLine(sb, style.Typography.DecorateTitle(node.Title), "beck-node-title", w / 2, top + titleLine / 2, m, style, FontRole.PillTitle, guard);
        if (node.Subtitle is { } sub)
            CenterLine(sb, sub, "beck-node-subtitle", w / 2, top + titleLine + gap + subLine / 2, m, style, FontRole.PillSubtitle, guard);
    }

    private static void EmitClass(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard)
    {
        StyleGeometry geo = style.Geometry;
        double bi = geo.NodeBorderInset;
        string clip = $"cc-{SvgWriter.Attr(node.Id)}-{hash}";
        sb.Append($"<clipPath id=\"{clip}\"><rect x=\"0\" y=\"0\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(geo.ClassRadius)}\"/></clipPath>");
        sb.Append(Artwork.Rect(style, "beck-node beck-node--class", bi, bi, w - 2 * bi, h - 2 * bi, geo.ClassRadius, hash + ":" + node.Id, shadow: true));
        sb.Append($"<g clip-path=\"url(#{clip})\">");

        double stereoLine = geo.StereoLine, titleLine = geo.ClassTitleLine, headPadY = geo.HeadPadY / 2,
               memberLine = geo.MemberLine, sectionPadY = geo.SectionPadY / 2, memberGap = geo.MemberGap;
        bool hasStereo = node.Stereotype != null;
        double headH = (hasStereo ? stereoLine : 0) + titleLine + headPadY * 2 + geo.HeadBorderBottom;
        sb.Append($"<rect class=\"beck-class-head\" x=\"0\" y=\"0\" width=\"{N(w)}\" height=\"{N(headH)}\"/>");
        double ty = headPadY;
        if (hasStereo)
        {
            CenterLine(sb, $"«{node.Stereotype}»", "beck-class-stereo", w / 2, ty + stereoLine / 2, m, style, FontRole.ClassStereotype, guard);
            ty += stereoLine;
        }
        CenterLine(sb, style.Typography.DecorateTitle(node.Title), "beck-class-title", w / 2, ty + titleLine / 2, m, style, FontRole.ClassTitle, guard);
        sb.Append($"<line class=\"beck-class-head-border\" x1=\"0\" y1=\"{N(headH)}\" x2=\"{N(w)}\" y2=\"{N(headH)}\"/>");

        FontRoleSpec memberSpec = style.Typography.Roles.Of(FontRole.ClassMember);
        double memberX = geo.SectionPadX / 2;
        double y = headH;
        bool prior = false;
        foreach (var (members, css) in new[] { (node.Fields, "beck-class-field"), (node.Methods, "beck-class-method") })
        {
            if (members.Count == 0) continue;
            if (prior) { sb.Append($"<line class=\"beck-class-divider\" x1=\"0\" y1=\"{N(y)}\" x2=\"{N(w)}\" y2=\"{N(y)}\"/>"); y += geo.HeadBorderBottom; }
            double my = y + sectionPadY;
            foreach (var member in members)
            {
                sb.Append($"<text class=\"{css}\" x=\"{N(memberX)}\" y=\"{N(my + memberLine / 2)}\" font-size=\"{N(memberSpec.SizePx)}\" font-weight=\"{P(memberSpec.Weight)}\" ")
                  .Append($"dominant-baseline=\"central\" text-anchor=\"start\" style=\"font-family:var(--beck-font-mono)\">{SvgWriter.Text(member)}</text>");
                my += memberLine + memberGap;
            }
            y += members.Count * memberLine + (members.Count - 1) * memberGap + sectionPadY * 2;
            prior = true;
        }
        sb.Append("</g>");
    }

    private static void CenterLine(StringBuilder sb, string text, string cls, double cx, double cy, ITextMeasurer m, BeckStyle style, FontRole role, bool guard)
    {
        FontRoleSpec spec = style.Typography.Roles.Of(role);
        double tl = m.Measure(text, role, spec).Width;
        sb.Append($"<text class=\"{cls}\" x=\"{N(cx)}\" y=\"{N(cy)}\" font-size=\"{N(spec.SizePx)}\" font-weight=\"{P(spec.Weight)}\" ")
          .Append($"dominant-baseline=\"central\" text-anchor=\"middle\"{Guard(tl, guard)}>")
          .Append(SvgWriter.Text(Cased(spec, text))).Append("</text>");
    }

    private static void EmitCard(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m,
        int idx, string hash, BeckStyle style, bool guard, IReadOnlyList<(string Text, string Color)>? states)
    {
        StyleGeometry geo = style.Geometry;
        double bi = geo.NodeBorderInset;
        string cls = "beck-node";
        if (node.Kind == NodeKind.External) cls += " beck-node--external";
        if (node.Variant == NodeVariant.Subtle) cls += " beck-node--subtle";
        sb.Append(Artwork.Rect(style, cls, bi, bi, w - 2 * bi, h - 2 * bi, geo.CardRadius, hash + ":" + node.Id, shadow: true));

        bool hasIcon = Icons.ResolveIcon(node.Icon) != null;
        double padHalf = geo.CardPadX / 2;
        double textX = padHalf + (hasIcon ? geo.IconW + geo.IconGap : 0);
        if (hasIcon)
        {
            double chipY = h / 2 - geo.IconW / 2;
            sb.Append($"<rect class=\"beck-icon-chip\" x=\"{N(padHalf)}\" y=\"{N(chipY)}\" width=\"{N(geo.IconW)}\" height=\"{N(geo.IconW)}\" rx=\"{N(geo.IconChipRadius)}\"/>");
            sb.Append(IconSvg(node.Icon!, padHalf + 7, chipY + 7, 20));
        }

        double statusChipH = geo.StatusChipH;
        double titleLine = geo.CardTitleLine, subLine = geo.CardSubLine, textGap = geo.TextGap;
        // Wrap the title/subtitle into the SAME lines CardSizer measured the box for, so the drawn
        // text stays inside it (a single overflowing <text> was the "text escapes its box" bug).
        double avail = CardSizer.CardTextAvail(node, m, geo, style.Typography.Roles, style.Typography.TitlePrefix, style.Typography.TitleSuffix);
        var titleLines = CardSizer.WrapText(m, style.Typography.DecorateTitle(node.Title), FontRole.CardTitle, avail, style.Typography.Roles);
        var subLines = node.Subtitle != null ? CardSizer.WrapText(m, node.Subtitle, FontRole.CardSubtitle, avail, style.Typography.Roles) : null;
        double textColH = titleLines.Count * titleLine
            + (subLines != null ? textGap + subLines.Count * subLine : 0)
            + (node.Status != null ? textGap + geo.StatusMt + statusChipH : 0);
        double top = h / 2 - textColH / 2;
        double stackY = top;
        foreach (string line in titleLines)
        {
            Line(sb, line, "beck-node-title", textX, stackY + titleLine / 2, m, style, FontRole.CardTitle, guard);
            stackY += titleLine;
        }
        if (subLines != null)
        {
            stackY += textGap;
            foreach (string line in subLines)
            {
                Line(sb, line, "beck-node-subtitle", textX, stackY + subLine / 2, m, style, FontRole.CardSubtitle, guard);
                stackY += subLine;
            }
        }
        bool hasStates = states is { Count: > 1 };
        if (node.Status is { } || hasStates)
        {
            double sy = stackY + textGap + geo.StatusMt;
            if (hasStates)
            {
                // The flow swaps this pill: pre-build one group per (text,color) state,
                // state 0 (resting, possibly empty) visible, the rest hidden — the
                // compiler cross-fades. A status-less target overhangs (accepted, §15).
                for (int si = 0; si < states!.Count; si++)
                {
                    sb.Append($"<g class=\"beck-status-state bss{idx}-{si}-{hash}\"{(si == 0 ? "" : " opacity=\"0\"")}>");
                    if (states[si].Text.Length > 0) StatusPill(sb, states[si].Text, states[si].Color, textX, sy, statusChipH, m, style, guard);
                    sb.Append("</g>");
                }
            }
            else
            {
                string status = node.Status!;
                FontRoleSpec statusSpec = style.Typography.Roles.Of(FontRole.Status);
                double sw = m.Measure(status, FontRole.Status, statusSpec).Width;
                sb.Append($"<rect class=\"beck-status-bg\" x=\"{N(textX)}\" y=\"{N(sy)}\" width=\"{N(sw + 16)}\" height=\"{N(statusChipH)}\" rx=\"{N(statusChipH / 2)}\"/>");
                sb.Append($"<text class=\"beck-status-text\" x=\"{N(textX + 8)}\" y=\"{N(sy + statusChipH / 2)}\" font-size=\"{N(statusSpec.SizePx)}\" font-weight=\"{P(statusSpec.Weight)}\" dominant-baseline=\"central\" text-anchor=\"start\"{Guard(sw, guard)}>{SvgWriter.Text(Cased(statusSpec, status))}</text>");
            }
        }
    }

    private static void EmitGhost(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard)
    {
        StyleGeometry geo = style.Geometry;
        double bi = geo.NodeBorderInset;
        sb.Append(Artwork.Rect(style, "beck-node beck-node--ghost", bi, bi, w - 2 * bi, h - 2 * bi, geo.GhostRadius, hash + ":" + node.Id));
        bool hasIcon = Icons.ResolveIcon(node.Icon) != null;
        double rowH = Math.Max(hasIcon ? geo.GhostIcon : 0, geo.GhostLabelLine);
        double rowTop = h / 2 - (node.Status != null ? (rowH + geo.TextGap + geo.StatusInlineLine) / 2 : rowH / 2);
        double ghostPadHalf = geo.GhostPadX / 2;
        double labelX = ghostPadHalf + (hasIcon ? geo.GhostIcon + geo.GhostIconGap : 0);
        if (hasIcon) sb.Append(IconSvg(node.Icon!, ghostPadHalf, rowTop + rowH / 2 - 7, 14));
        Line(sb, style.Typography.DecorateTitle(node.Title), "beck-ghost-label", labelX, rowTop + rowH / 2, m, style, FontRole.GhostLabel, guard);
    }

    private static void Line(StringBuilder sb, string text, string cls, double x, double cy, ITextMeasurer m, BeckStyle style, FontRole role, bool guard)
    {
        FontRoleSpec spec = style.Typography.Roles.Of(role);
        double tl = m.Measure(text, role, spec).Width;
        sb.Append($"<text class=\"{cls}\" x=\"{N(x)}\" y=\"{N(cy)}\" font-size=\"{N(spec.SizePx)}\" font-weight=\"{P(spec.Weight)}\" ")
          .Append($"dominant-baseline=\"central\" text-anchor=\"start\"{Guard(tl, guard)}>")
          .Append(SvgWriter.Text(Cased(spec, text))).Append("</text>");
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
