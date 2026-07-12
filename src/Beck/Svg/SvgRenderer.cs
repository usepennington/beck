using System.Text;
using Beck.Animate;
using Beck.Layout;
using Beck.Model;
using Beck.Route;
using Beck.Text;

namespace Beck.Svg;

/// <summary>
/// Assembles the static SVG document for a diagram: theming <c>&lt;style&gt;</c>,
/// marker <c>&lt;defs&gt;</c>, title block, and the layered z-order (groups →
/// edges → nodes → group labels). §8. Animation (§9–10) lands in M8/M9.
/// </summary>
internal static class SvgRenderer
{
    private static string N(double n) => SvgWriter.Num(n);

    private static string P(int n) => SvgWriter.Int(n);

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
        var geo = style.Geometry;
        // The per-text textLength guard emission mode. All → always (default, byte-identical);
        // Off → never; FallbackOnly → only when the measurer approximates (embedded metrics table).
        var guard = options.TextLengthGuard switch
        {
            TextLengthGuard.Off => false,
            TextLengthGuard.FallbackOnly => measurer.IsApproximate,
            _ => true,
        };
        var markers = new Markers(hash);
        // Pill states a node's flow will swap through (null unless animating with >1 state).
        var statusMap = StatusStates.Build(model);
        var animating = options.Animation is AnimationMode.Full or AnimationMode.Scrub && model.Meta.Animate;
        IReadOnlyList<(string Text, string Color)>? StatesFor(string id) =>
            animating && statusMap.TryGetValue(id, out var s) && s.Count > 1 ? s : null;
        // The non-empty pill texts a node's flow swaps through — sized into the card up front
        // (row height + widest pill), since compiled CSS can't grow the box the way the live-DOM
        // engine could when a status landed on a status-less node.
        var mindMap = model.Meta.Type == DiagramType.MindMap;
        var sizes = model.Nodes.ToDictionary(n => n.Id, n => CardSizer.Measure(n, measurer, geo, style.Typography.Roles, style.Typography.TitlePrefix, style.Typography.TitleSuffix, NonEmptyStatusTexts(StatesFor(n.Id)), mindMap));
        var extraDefs = "";
        // Per-edge gradient <defs> collected by Edge() when the style paints luminous gradient edges
        // (glow). One userSpaceOnUse gradient per gradient-stroked edge, run along that edge's own
        // endpoints — see Edge() for why per-edge (the degenerate-bbox fix).
        var edgeDefs = new StringBuilder();
        // Per-edge overlay specs (comet/draw-on/marching) collected by the edge/message painters when the
        // style opts in; compiled to motion CSS below. Empty for classic (Overlay=None) — byte-identical.
        var overlays = new List<EdgeOverlaySpec>();
        LayoutResult layout;
        var body = new StringBuilder();
        List<FlowEdge> flowEdges;
        SeqChoreo? choreo = null;

        if (model.Meta.Type == DiagramType.Sequence)
        {
            var seq = SequenceLayout.Compute(model, sizes);
            layout = seq.AsLayout();
            var painter = new SequencePainter(hash, markers, measurer, style);
            body.Append("<g class=\"beck-overlay\">").Append(painter.Render(model, seq, animating)).Append("</g>");
            extraDefs = painter.Defs;
            flowEdges = painter.MessageEdges;
            overlays.AddRange(painter.Overlays);
            // Sequence storytelling applies only to a DERIVED flow (authored message order).
            if (model.Flow.Derived)
            {
                choreo = new SeqChoreo(
                    seq.Activations.Select(a => (a.StartEdge, a.EndEdge)).ToList(),
                    seq.Bands.Count);
            }

            body.Append("<g class=\"beck-nodes\">");
            for (var i = 0; i < model.Nodes.Count; i++)
            {
                if (layout.Nodes.TryGetValue(model.Nodes[i].Id, out var r))
                {
                    body.Append(Node(model.Nodes[i], r, measurer, hash, i, style, guard, StatesFor(model.Nodes[i].Id), mindMap));
                }
            }

            body.Append("</g>");
        }
        else if (model.Meta.Type == DiagramType.Chart)
        {
            // Charts skip the whole node/edge/flow pipeline: the painter draws straight to SVG, and a
            // dummy LayoutResult carries only its size into the shared shell (title + token <style>).
            // meta.Animate is forced false in the builder, so the animation gates below never fire.
            var (chartBody, chartW, chartH) = ChartPainter.Render(model.Chart!, measurer, guard);
            layout = new LayoutResult(new Dictionary<string, Rect>(), new Dictionary<string, Rect>(), chartW, chartH);
            body.Append(chartBody);
            flowEdges = [];
        }
        else
        {
            layout = model.Meta.Type == DiagramType.MindMap
                ? MindMapLayout.Compute(model, sizes)
                : LayeredLayout.Compute(model, sizes);
            var edges = EdgePainter.RouteEdges(model, layout);
            // The effective (possibly bowed) path per edge — computed once and reused for both the
            // rendered edge and its FlowEdge, so a packet rides exactly the drawn curve. At classic
            // (BowAmplitude 0) this is e.D verbatim, so flowEdges + edges stay byte-identical.
            var effD = edges.Select(e => Shaping.EdgePath(style, e.D, e.Points, hash + ":" + e.Edge.Id)).ToList();
            flowEdges = edges.Select((e, i) => new FlowEdge(e.Edge.Id, e.Edge.From, e.Edge.To, e.Edge.Kind, effD[i])).ToList();

            // z1 groups (largest-area first so nested boxes stack on top)
            body.Append("<g class=\"beck-groups\">");
            foreach (var g in model.Groups.Where(g => layout.Groups.ContainsKey(g.Id))
                         .OrderByDescending(g => layout.Groups[g.Id].W * layout.Groups[g.Id].H))
            {
                var r = layout.Groups[g.Id];
                body.Append(Artwork.Rect(style, "beck-group", r.X, r.Y, r.W, r.H, geo.GroupRadius, hash + ":" + g.Id,
                    $"stroke:color-mix(in srgb, {SvgWriter.Attr(g.Accent)} {P(style.Mix.GroupBorder)}%, transparent)"));
                // Blueprint's technical-drawing dimension line along the group's top edge (no-op for
                // every other style / a zero DimensionTick — byte-identical).
                body.Append(Artwork.GroupDimension(style, r.X, r.Y, r.W));
            }
            body.Append("</g>");

            // z2 edges (+ markers), then labels (pass 2 — mid labels dodge every line)
            body.Append("<g class=\"beck-overlay\">");
            for (var i = 0; i < edges.Count; i++)
            {
                body.Append(Edge(edges[i], effD[i], markers, style, hash, i, edgeDefs, animating, overlays));
            }

            var placer = new LabelPlacer(layout.Nodes.Values, layout.Width, layout.Height, style, guard);
            foreach (var e in edges)
            {
                if (!string.IsNullOrEmpty(e.Edge.FromLabel))
                {
                    body.Append(placer.EndLabel(e.Points, e.Edge.FromLabel!, true, measurer));
                }

                if (!string.IsNullOrEmpty(e.Edge.ToLabel))
                {
                    body.Append(placer.EndLabel(e.Points, e.Edge.ToLabel!, false, measurer));
                }
            }
            // Built once — MidLabel skips this edge's own index rather than each call allocating a
            // fresh (E-1)-element list of every other edge's polyline.
            var edgeLines = edges.Select(o => o.Points).ToList();
            for (var i = 0; i < edges.Count; i++)
            {
                if (string.IsNullOrEmpty(edges[i].Edge.Label))
                {
                    continue;
                }

                body.Append(placer.MidLabel(edges[i].Points, edges[i].Edge.Label!, edgeLines, i, measurer));
            }
            body.Append("</g>");

            // z3 nodes
            body.Append("<g class=\"beck-nodes\">");
            for (var i = 0; i < model.Nodes.Count; i++)
            {
                if (layout.Nodes.TryGetValue(model.Nodes[i].Id, out var r))
                {
                    body.Append(Node(model.Nodes[i], r, measurer, hash, i, style, guard, StatesFor(model.Nodes[i].Id), mindMap));
                }
            }

            body.Append("</g>");

            // z4 group labels
            body.Append("<g class=\"beck-group-labels\">");
            foreach (var g in model.Groups.Where(g => layout.Groups.ContainsKey(g.Id) && !string.IsNullOrEmpty(g.Label)))
            {
                var r = layout.Groups[g.Id];
                var glSpec = style.Typography.Roles.Of(FontRole.GroupLabel);
                var lw = measurer.Measure(g.Label, FontRole.GroupLabel, glSpec).Width;
                double lx = r.X + 14, ly = r.Y;
                body.Append($"<rect class=\"beck-group-label-bg\" x=\"{N(lx - 5.6)}\" y=\"{N(ly - 8)}\" width=\"{N(lw + 11.2)}\" height=\"16\" rx=\"{N(geo.GroupLabelBgRadius)}\"/>");
                body.Append($"<text class=\"beck-group-label\" x=\"{N(lx)}\" y=\"{N(ly)}\" dominant-baseline=\"central\" ")
                    .Append($"font-size=\"{N(glSpec.SizePx)}\" font-weight=\"{P(glSpec.Weight)}\" letter-spacing=\"{SvgWriter.Ls(glSpec.LetterSpacingEm)}\" style=\"fill:{SvgWriter.Attr(g.Accent)}\">{SvgWriter.Text(g.Label.ToUpperInvariant())}</text>");
            }
            body.Append("</g>");
        }

        var titleH = TitleHeight(model);
        var w = layout.Width;
        var bodyH = layout.Height;

        // Narration caption bar (§8.6): a teleprompter under the diagram whose beats
        // cross-fade as the story plays. Rendered here (the compiler animates the beats).
        var beats = model.Flow.Steps.OfType<NarrateStep>().ToList();
        var narrationActive = options.Animation is AnimationMode.Full or AnimationMode.Scrub && model.Meta.Animate
            && model.Meta.Narration.Enabled && beats.Count > 0;
        if (narrationActive)
        {
            var (barMarkup, blockH) = NarrationBar(beats, w, bodyH, measurer, hash, style);
            body.Append(barMarkup);
            bodyH += blockH;
        }

        var totalH = titleH + bodyH;
        // options.Font is the host's base font: it applies to the default (classic) style only.
        // A named style's Typography is part of its visual identity and wins over the host default.
        var styleFont = style.Name != BeckStyle.Classic.Name;
        var font = !styleFont && options.Font is { } f ? $"'{f.Family}', system-ui, sans-serif" : style.Typography.SansFamily;
        var mono = !styleFont && options.Font?.MonoFamily is { } mf ? $"'{mf}', ui-monospace, monospace" : style.Typography.MonoFamily;
        var theme = options.Theme ?? model.Meta.Theme;

        // Animation compiler (§9–10): simulate the flow → CSS keyframes + fx layer.
        // Full loops on load; Scrub drives the same keyframes off scroll position.
        string animCss = "", animDefs = "";
        var motionCss = "";
        if (options.Animation is AnimationMode.Full or AnimationMode.Scrub
            && model.Meta.Animate && model.Flow.Steps.Count > 0 && flowEdges.Count > 0)
        {
            var schedule = ScheduleBuilder.Build(model, flowEdges, style.Motion);
            var boxes = new List<NodeBox>(model.Nodes.Count);
            foreach (var n in model.Nodes)
            {
                boxes.Add(layout.Nodes.TryGetValue(n.Id, out var r) ? CardBox(n, r, geo) : default);
            }

            var compiler = new CssCompiler(schedule, hash, boxes, style.Motion, style.Strokes, choreo, options.Animation == AnimationMode.Scrub);
            body.Append(compiler.Markup());
            animDefs = compiler.Defs();
            motionCss += compiler.Css();
        }
        // Per-edge overlay motion (comet/draw-on/marching) — compiled independently of the flow schedule
        // (a glow diagram with edges but no flow still gets travelling comets). Empty for classic.
        motionCss += CssCompiler.EdgeOverlayCss(hash, overlays, style.Edges);
        if (!string.IsNullOrEmpty(motionCss))
        {
            animCss = "@media (prefers-reduced-motion:no-preference){" + motionCss + "}";
        }

        // meta.fit: `shrink` (default) lets the SVG scale down inside a narrow container
        // (the width attribute already caps it at natural size in wide ones); `scroll`
        // pins natural size so the host's overflow container scrolls instead.
        var maxWidth = model.Meta.Fit == FitMode.Scroll ? $"{N(w)}px" : "100%";
        var svg = new StringBuilder();
        svg.Append($"<svg class=\"beck-svg b-{hash}\" viewBox=\"0 0 {N(w)} {N(totalH)}\" width=\"{N(w)}\" height=\"{N(totalH)}\" ")
           .Append($"style=\"max-width:{maxWidth};height:auto\" font-family=\"var(--beck-font)\" role=\"img\" aria-label=\"{SvgWriter.Attr(model.Meta.Title ?? "diagram")}\">");
        svg.Append("<style>").Append(Stylesheet.Emit(hash, font, mono, theme, style, options.ThemeHooks, mindMap)).Append(animCss).Append("</style>");
        svg.Append("<defs>").Append(markers.Defs).Append(extraDefs).Append(animDefs).Append(edgeDefs).Append(Stylesheet.StyleDefs(hash, style)).Append("</defs>");
        svg.Append(TitleBlock(model, w, style));
        svg.Append($"<g class=\"beck-canvas\" transform=\"translate(0,{N(titleH)})\">").Append(body).Append("</g>");
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static double TitleHeight(DiagramModel model)
    {
        double h = 0;
        if (model.Meta.Title != null)
        {
            h += 36;      // text-2xl line (32) + mb-1 (4)
        }

        if (model.Meta.Subtitle != null)
        {
            h += 28;   // 0.9rem line (~20) + mb-2 (8)
        }

        return h;
    }

    private static string TitleBlock(DiagramModel model, double w, BeckStyle style)
    {
        if (model.Meta.Title is null && model.Meta.Subtitle is null)
        {
            return "";
        }

        var sb = new StringBuilder("<g class=\"beck-title-block\">");
        double cx = w / 2, y;
        if (model.Meta.Title is { } title)
        {
            var spec = style.Typography.Roles.Of(FontRole.DiagramTitle);
            y = 22;
            sb.Append($"<text class=\"beck-title\" x=\"{N(cx)}\" y=\"{N(y)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
              .Append($"font-size=\"{N(spec.SizePx)}\" font-weight=\"{P(spec.Weight)}\" letter-spacing=\"{SvgWriter.Ls(spec.LetterSpacingEm)}\">{SvgWriter.Text(title)}</text>");
        }
        if (model.Meta.Subtitle is { } sub)
        {
            var spec = style.Typography.Roles.Of(FontRole.DiagramSubtitle);
            double sy = (model.Meta.Title != null ? 36 : 0) + 14;
            sb.Append($"<text class=\"beck-subtitle\" x=\"{N(cx)}\" y=\"{N(sy)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
              .Append($"font-size=\"{N(spec.SizePx)}\">{SvgWriter.Text(sub)}</text>");
        }
        return sb.Append("</g>").ToString();
    }

    /// <summary>
    /// The narration bar (§8.6): one pre-built <c>&lt;g class="beck-beat"&gt;</c> per
    /// caption, stacked at the same spot; the compiler cross-fades them. Each reserves two
    /// lines so the layout never jumps as beats come and go. The first beat renders visible
    /// (no markup <c>opacity</c>) as the designated static caption, so a reduced-motion viewer
    /// — who gets none of the motion-guarded keyframes — sees the opening caption instead of an
    /// empty reserved block; its keyframe still starts at <c>opacity:0</c> and drives the fade
    /// under motion. Beats 2..N stay markup <c>opacity="0"</c> (their reveal is animation-only,
    /// exactly like flow packets/trails), so reduced motion never stacks every caption at once.
    /// </summary>
    private static (string Markup, double BlockH) NarrationBar(
        IReadOnlyList<NarrateStep> beats, double canvasW, double topY, ITextMeasurer m, string hash, BeckStyle style)
    {
        const double Margin = 14.4, PadX = 18.4, PadY = 9.6, LineHt = 14.72 * 1.45,
                     DotR = 3.5, DotD = 7, MinContentH = 40.48;
        // The bullet→text gap is a style field (terminal widens it for its mono caption); classic = 9.6.
        var gap = style.Geometry.NarrationBulletGap;
        var barW = Math.Min(736, 0.92 * canvasW);
        var barH = Math.Max(MinContentH, 2 * LineHt) + 2 * PadY;
        double barTop = topY + Margin, cx = canvasW / 2, barLeft = cx - barW / 2, midY = barTop + barH / 2;
        var maxTextW = barW - 2 * PadX - DotD - gap;

        var sb = new StringBuilder();
        var nSpec = style.Typography.Roles.Of(FontRole.Narration);
        var figCaption = style.Typography.NarrationFigureCaption;
        // Editorial renders the caption as a numbered figure caption: a deterministic "Fig. N — "
        // prefix (N = 1-based beat order, content-derived + stable) and serif-italic text. The prefix
        // is prepended before wrapping/measuring so the measured string == the rendered string.
        var textStyle = figCaption ? "fill:currentColor;font-style:italic" : "fill:currentColor";
        for (var i = 0; i < beats.Count; i++)
        {
            var beatText = figCaption
                ? $"Fig. {(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)} — {beats[i].Text}"
                : beats[i].Text;
            var lines = WrapNarration(beatText, maxTextW, m, style);
            var textW = lines.Max(l => m.Measure(l, FontRole.Narration, nSpec).Width);
            var left = cx - (DotD + gap + textW) / 2;
            var textCx = left + DotD + gap + textW / 2;
            var firstY = midY - (lines.Count - 1) * LineHt / 2;
            var color = beats[i].Color ?? "var(--beck-text)";

            // Beat 0 stays visible in markup (the static caption reduced-motion sees); beats 2..N hide
            // like packets — the motion-guarded keyframes reveal them, so the invariant holds both ways.
            var hidden = i == 0 ? "" : " opacity=\"0\"";
            sb.Append($"<g class=\"beck-beat bbeat{i}-{hash}\"{hidden} style=\"color:{color}\">");
            if (figCaption)
            {
                // Editorial's figure caption: no filled/bordered bar (a solid surface-filled box reads
                // as a black block on a dark page, at odds with the serif "Fig. N —" identity). Instead a
                // transparent surface with a single hairline rule above the caption — the print-figure
                // separator that matches the sequence caption. The bullet dot + serif text carry the rest.
                sb.Append($"<line class=\"beck-narration-rule\" x1=\"{N(barLeft)}\" y1=\"{N(barTop)}\" x2=\"{N(barLeft + barW)}\" y2=\"{N(barTop)}\" ")
                  .Append($"style=\"stroke:color-mix(in srgb,var(--beck-primary) {P(style.Mix.NarrationBorder)}%,var(--beck-text-faint));stroke-width:{N(style.Geometry.HairlineStroke)}\"/>");
            }
            else
            {
                sb.Append($"<rect class=\"beck-narration-bar\" x=\"{N(barLeft)}\" y=\"{N(barTop)}\" width=\"{N(barW)}\" height=\"{N(barH)}\" rx=\"{N(style.Geometry.NarrationRadius)}\" ")
                  .Append($"style=\"fill:color-mix(in srgb,var(--beck-primary) {P(style.Mix.NarrationFill)}%,var(--beck-surface));stroke:color-mix(in srgb,var(--beck-primary) {P(style.Mix.NarrationBorder)}%,transparent);filter:{style.Geometry.NarrationShadow}\"/>");
            }

            sb.Append($"<circle cx=\"{N(left + DotR)}\" cy=\"{N(midY)}\" r=\"{N(DotR)}\" style=\"fill:currentColor\" opacity=\"0.55\"/>");
            for (var k = 0; k < lines.Count; k++)
            {
                sb.Append($"<text class=\"beck-narration-text\" x=\"{N(textCx)}\" y=\"{N(firstY + k * LineHt)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
                  .Append($"font-size=\"{N(nSpec.SizePx)}\" font-weight=\"{P(nSpec.Weight)}\" style=\"{textStyle}\">{SvgWriter.Text(lines[k])}</text>");
            }

            sb.Append("</g>");
        }
        return (sb.ToString(), Margin + barH);
    }

    /// <summary>Greedy word-wrap into ≤2 lines at the Narration role (overflow folds into line 2).
    /// Reuses <see cref="CardSizer.WrapText"/>'s greedy loop (same measurer/role/avail contract);
    /// narration just folds anything past 2 lines into the second line instead of letting it grow.</summary>
    private static List<string> WrapNarration(string text, double maxW, ITextMeasurer m, BeckStyle style)
    {
        var lines = CardSizer.WrapText(m, text, FontRole.Narration, maxW, style.Typography.Roles);
        if (lines.Count > 2)
        {
            lines = [lines[0], string.Join(" ", lines.Skip(1))];
        }

        return lines;
    }

    private static string Edge(RoutedEdge e, string d, Markers markers, BeckStyle style, string hash, int idx,
        StringBuilder edgeDefs, bool motion, List<EdgeOverlaySpec> overlays)
    {
        var m = e.Edge;
        var es = style.Edges;
        var sb = new StringBuilder();
        // Metro's per-line palette (BaseColorPalette): an edge that uses the *default* colour
        // (var(--beck-edge)) takes the palette hue for its stable draw-order index, so each relationship
        // reads as its own transit-line colour. An author's explicit per-edge accent (any non-default
        // colour) wins — the palette is skipped for that edge. The effective colour drives the base
        // stroke, the arrowhead marker, and the metro station-dot rings together, so the whole line reads
        // one hue. Empty palette (classic, every non-metro style) → m.Color unchanged, byte-identical.
        // Gradient edges (glow) own the default-colour slot instead, so palette + gradient never collide.
        var edgeColor = style.Strokes.GradientEdges ? m.Color : es.BaseColorFor(idx, m.Color);
        // Glow's luminous edges: an edge that uses the *default* colour (var(--beck-edge)) paints with a
        // per-edge gradient. Author-coloured edges keep their explicit colour, and the arrow markers
        // (still edge-coloured) match the gradient's faint endpoints.
        //
        // The gradient MUST use gradientUnits="userSpaceOnUse" with the edge's own endpoint coords, NOT
        // the default objectBoundingBox: an axis-aligned straight edge (e.g. a horizontal Client→API
        // hop, d="M 336 180.5 L 216 180.5") has a zero-height/zero-width bounding box, and an
        // objectBoundingBox gradient over a degenerate bbox paints nothing — the edge vanishes. Running
        // the gradient along the edge's actual first→last route point keeps it always visible AND makes
        // the edge→bright-midpoint→edge bloom travel each connector (the intended look, with the faint
        // endpoints matching the edge-coloured arrow markers). One gradient per gradient-stroked edge.
        string stroke;
        if (style.Strokes.GradientEdges && m.Color == Defaults.EdgeColor && e.Points.Count > 0)
        {
            Point a = e.Points[0], b = e.Points[^1];
            var gid = $"beck-edge-grad-{hash}-{P(idx)}";
            edgeDefs.Append($"<linearGradient id=\"{gid}\" gradientUnits=\"userSpaceOnUse\" x1=\"{N(a.X)}\" y1=\"{N(a.Y)}\" x2=\"{N(b.X)}\" y2=\"{N(b.Y)}\">")
                    .Append("<stop offset=\"0\" stop-color=\"var(--beck-edge)\"/>")
                    .Append("<stop offset=\"0.5\" stop-color=\"color-mix(in srgb, var(--beck-info) 50%, var(--beck-accent))\"/>")
                    .Append("<stop offset=\"1\" stop-color=\"var(--beck-edge)\"/>")
                    .Append("</linearGradient>");
            stroke = $"url(#{gid})";
        }
        else
        {
            stroke = SvgWriter.Attr(edgeColor);
        }
        // Optional static trace-bed underlay (circuit's two-layer trace) — a wider, darker path sharing
        // this edge's exact d, emitted FIRST so it sits behind the base .beck-edge line. Static (no
        // animation); the base edge below stays the single continuous flow path. Off (width 0) → nothing.
        sb.Append(UnderlayPath(style, d));
        // Optional faint base-layer opacity (glow's dim rail under a bright comet). null → no attr (classic).
        var baseOp = es.BaseOpacity is { } bo ? $";stroke-opacity:{SvgWriter.Num(bo)}" : "";
        sb.Append($"<path class=\"beck-edge beck-edge--{Tokens.EdgeKind.Wire(m.Kind)}\" d=\"{d}\" ")
          .Append($"style=\"stroke:{stroke}{baseOp}\"");
        if (m.Style == EdgeStyle.Dashed)
        {
            sb.Append($" stroke-dasharray=\"{style.Strokes.EdgeDash}\"");
        }

        var end = m.MarkerEnd ?? (m.Arrow is ArrowEnds.End or ArrowEnds.Both ? MarkerShape.Arrow : null);
        var start = m.MarkerStart ?? (m.Arrow is ArrowEnds.Start or ArrowEnds.Both ? MarkerShape.Arrow : null);
        // Marker colour: the edge's own colour (classic), or the style's comet-hue override (glow) when
        // this edge uses the default colour — a bright arrowhead over a faint slate base rail.
        var markerColor = es.MarkerColor is { } mkc && m.Color == Defaults.EdgeColor ? mkc : edgeColor;
        if (end is { } ee)
        {
            sb.Append($" marker-end=\"url(#{markers.Ensure(markerColor, ee, es, style.Geometry.EdgeStroke)})\"");
        }

        if (start is { } ss)
        {
            sb.Append($" marker-start=\"url(#{markers.Ensure(markerColor, ss, es, style.Geometry.EdgeStroke)})\"");
        }

        sb.Append($" data-edge=\"{SvgWriter.Attr(m.Id)}\"/>");
        // Optional overlay layer (comet/draw-on/marching) — an additional path sharing this edge's exact
        // d, never a split of the base edge. Only when motion is live and the style opts in; the compiled
        // keyframes come from CssCompiler.EdgeOverlayCss (collected via `overlays`). Classic (None) adds nothing.
        if (motion && es.Overlay != EdgeOverlay.None)
        {
            var (markup, spec) = OverlayPath(style, d, idx, hash);
            sb.Append(markup);
            overlays.Add(spec);
        }
        // Circuit's via dots: a small circle at every genuine bend of the edge's already-computed route
        // polyline (the elbow where a right-angle trace turns). Read straight off the existing route
        // geometry (e.Points) — the router is untouched and the edge stays one continuous <path> above;
        // the vias are additional sibling elements drawn on top of the trace. Deterministic (geometry
        // only, no RNG). Every other style emits nothing here.
        if (style.Artwork == StyleArtwork.Circuit)
        {
            foreach (var b in Bends(e.Points))
            {
                sb.Append($"<circle class=\"beck-via\" cx=\"{N(b.X)}\" cy=\"{N(b.Y)}\" r=\"{N(style.Geometry.ViaRadius)}\" style=\"fill:var(--beck-via, var(--beck-edge))\"/>");
            }
        }
        // Metro's station dots: a white-filled, edge-coloured-ring circle at each of the edge's two
        // anchor endpoints (the route polyline's first + last point), drawn over the thick line. Read
        // from the existing route geometry — router untouched, edge still one continuous <path> above.
        if (style.Artwork == StyleArtwork.Metro && e.Points.Count > 0)
        {
            sb.Append(Artwork.Station(style, e.Points[0].X, e.Points[0].Y, edgeColor));
            sb.Append(Artwork.Station(style, e.Points[^1].X, e.Points[^1].Y, edgeColor));
        }
        return sb.ToString();
    }

    /// <summary>
    /// The static <em>trace-bed underlay</em> for one edge/message (<see cref="StyleEdges.UnderlayWidth"/>):
    /// a second, wider, darker <c>&lt;path&gt;</c> sharing the edge's exact <paramref name="d"/>, meant to
    /// be emitted <em>before</em> the base <c>.beck-edge</c> path so the thin bright line reads as a trace
    /// riding a dark bed (circuit's two-layer trace). Static — no animation, emitted regardless of motion;
    /// the base edge stays the one continuous flow path that packets/trails ride. Token-coloured through
    /// <c>var(--beck-edge-underlay, var(--beck-edge))</c> when the style leaves
    /// <see cref="StyleEdges.UnderlayColor"/> unset, so it theme-adapts and emits no resolved literal.
    /// <c>UnderlayWidth</c> ≤ 0 (classic, and every style that doesn't set it) returns <c>""</c> —
    /// byte-identical. Shared by the architecture edge painter and the sequence message painter.
    /// </summary>
    internal static string UnderlayPath(BeckStyle style, string d)
    {
        var e = style.Edges;
        if (e.UnderlayWidth <= 0)
        {
            return "";
        }

        var color = e.UnderlayColor.Length > 0 ? e.UnderlayColor : "var(--beck-edge-underlay, var(--beck-edge))";
        return $"<path class=\"beck-edge-bed\" d=\"{d}\" "
             + $"style=\"fill:none;stroke:{SvgWriter.Attr(color)};stroke-width:{N(e.UnderlayWidth)};stroke-linecap:{SvgWriter.Attr(e.BaseLinecap)};stroke-linejoin:round\"/>";
    }

    internal static (string Markup, EdgeOverlaySpec Spec) OverlayPath(BeckStyle style, string d, int idx, string hash)
    {
        var e = style.Edges;
        var len = PathLength.Of(d);
        var phase = Shaping.Phase(hash + ":o:" + P(idx));
        var dash = e.Overlay switch
        {
            EdgeOverlay.Comet => $"stroke-dasharray:{N(e.CometDash)} {N(len)};stroke-dashoffset:{N(phase * (e.CometDash + len))}",
            EdgeOverlay.DrawOn => $"stroke-dasharray:{N(len)} {N(len)};stroke-dashoffset:{N(len)}",
            _ => $"stroke-dasharray:{N(e.CometDash)} {N(e.CometDash)}",
        };
        // Per-edge comet colour (glow alternates cyan/light-cyan/violet by draw order); the single
        // --beck-edge-overlay token is the fallback for a palette-less overlay style (sketch's DrawOn).
        var color = e.OverlayPalette.Count > 0
            ? StyleEdges.Cycle(e.OverlayPalette, idx)
            : "var(--beck-edge-overlay, var(--beck-accent))";
        var bloom = e.OverlayBloom.Length > 0 ? $"filter:{SvgWriter.Attr(e.OverlayBloom)};" : "";
        var markup = $"<path class=\"beck-edge-overlay beo{idx}-{hash}\" d=\"{d}\" "
            + $"style=\"fill:none;stroke:{SvgWriter.Attr(color)};stroke-width:{N(e.OverlayWidth)};stroke-linecap:{SvgWriter.Attr(e.OverlayLinecap)};{bloom}{dash}\"/>";
        return (markup, new EdgeOverlaySpec(idx, e.Overlay, len, phase));
    }

    /// <summary>The non-empty pill texts of a node's flow-status states — the ONE filter both the
    /// sizing pass (CardSizer's <c>flowStatuses</c>) and the card painter derive from, so the reserved
    /// status row and the drawn pills can never disagree. <c>null</c> when nothing needs reserving.</summary>
    private static IReadOnlyList<string>? NonEmptyStatusTexts(IReadOnlyList<(string Text, string Color)>? states)
    {
        var texts = states?.Select(s => s.Text).Where(t => t.Length > 0).ToList();
        return texts is { Count: > 0 } ? texts : null;
    }

    /// <summary>The genuine bends (interior direction-changes) of a route polyline — the elbows where a
    /// step-round trace turns. <see cref="Shaping.Simplify"/> already reduces the polyline to
    /// <c>[first, …corners…, last]</c> via the same dedupe + collinearity test the router uses, so the
    /// bends are exactly its interior elements — no separate geometry pass needed.</summary>
    private static IEnumerable<Point> Bends(IReadOnlyList<Point> pts)
    {
        var simplified = Shaping.Simplify(pts);
        for (var i = 1; i < simplified.Count - 1; i++)
        {
            yield return simplified[i];
        }
    }

    private static string Node(NodeModel node, Rect rect, ITextMeasurer m, string hash, int idx, BeckStyle style,
        bool guard, IReadOnlyList<(string Text, string Color)>? statusStates = null, bool mindMap = false)
    {
        var sb = new StringBuilder();
        var accentStyle = $"--beck-accent:{node.Accent}";
        if (node.Surface != null)
        {
            accentStyle += $";--beck-node-bg:{node.Surface}";
        }

        if (node.TextColor != null)
        {
            accentStyle += $";--beck-text:{node.TextColor}";
        }
        // A linked node wraps in an SVG <a> so the whole card is clickable; the wrapper
        // sits outside the fx group so effect transforms don't disturb the hit area.
        if (node.Href != null)
        {
            sb.Append($"<a href=\"{SvgWriter.Attr(node.Href)}\"");
            if (node.Target != null)
            {
                sb.Append($" target=\"{SvgWriter.Attr(node.Target)}\"");
            }

            sb.Append('>');
        }
        // bn{idx} lets the animation compiler target this node's fx wrapper; the inner
        // .beck-fx-node isolates effect transforms (scale/shake) from the positioning
        // translate so pulses/highlights/fails bounce the card in place (§10.2).
        sb.Append($"<g class=\"beck-node-wrap bn{idx}-{hash}\" data-node=\"{SvgWriter.Attr(node.Id)}\" transform=\"translate({N(rect.X)},{N(rect.Y)})\" style=\"{SvgWriter.Attr(accentStyle)}\">");
        sb.Append("<g class=\"beck-fx-node\">");

        double w = rect.W, h = rect.H;
        // Mindmap nodes use depth-role emitters: a leaf pill (accent-tinted), or a root/rank-1 heading card
        // (icon chip + semantic status / ghost "planned"). A content card (items/body) and any other shape
        // fall through to the shared emitters below.
        if (mindMap && EmitMindMapNode(sb, node, w, h, m, hash, style, guard))
        {
            sb.Append("</g></g>");
            if (node.Href != null)
            {
                sb.Append("</a>");
            }

            return sb.ToString();
        }

        switch (node.Shape)
        {
            case NodeShape.Pill: EmitPill(sb, node, w, h, m, hash, style, guard); break;
            case NodeShape.Start: sb.Append(Artwork.Circle(style, "beck-node--start", w / 2, h / 2, 8, hash + ":" + node.Id)); break;
            case NodeShape.End:
                sb.Append(Artwork.Circle(style, "beck-node--end", w / 2, h / 2, 7, hash + ":" + node.Id))
                  .Append(Artwork.Circle(style, "beck-end-dot", w / 2, h / 2, 3.5, hash + ":" + node.Id + ":dot"));
                break;
            case NodeShape.Class: EmitClass(sb, node, w, h, m, hash, style, guard); break;
            case NodeShape.Diamond: EmitDiamond(sb, node, w, h, m, hash, style, guard); break;
            case NodeShape.Parallelogram: EmitParallelogram(sb, node, w, h, m, hash, style, guard); break;
            default:
                if (node.Variant == NodeVariant.Ghost || node.Kind == NodeKind.Ghost)
                {
                    EmitGhost(sb, node, w, h, m, hash, style, guard);
                }
                else
                {
                    EmitCard(sb, node, w, h, m, idx, hash, style, guard, statusStates);
                }

                break;
        }

        sb.Append("</g></g>");
        if (node.Href != null)
        {
            sb.Append("</a>");
        }

        return sb.ToString();
    }

    /// <summary>The node's card box in canvas coords + corner radius, for effect overlays.</summary>
    /// <remarks>Note the intentional quirk (preserved for byte-identity): there is no ghost branch,
    /// so ghost nodes fall to <see cref="StyleGeometry.CardRadius"/> (14) here even though their rect
    /// renders at <see cref="StyleGeometry.GhostRadius"/> (16).</remarks>
    private static NodeBox CardBox(NodeModel node, Rect r, StyleGeometry geo)
    {
        var rx = node.Shape switch
        {
            NodeShape.Pill => r.H / 2,
            NodeShape.Class => geo.ClassRadius,
            NodeShape.Start or NodeShape.End => Math.Min(r.W, r.H) / 2,
            // Diamond/parallelogram have sharp corners; the fx overlay box is a plain rect, so 0.
            NodeShape.Diamond or NodeShape.Parallelogram => 0,
            _ => geo.CardRadius,
        };
        var inset = geo.NodeBorderInset;
        return new NodeBox(r.X + inset, r.Y + inset, r.W - 2 * inset, r.H - 2 * inset, rx);
    }

    /// <summary>Shared status-chip markup (rect + text at the Status role, measured/cased once) for both
    /// the flow-status pill (inline-styled, no class) and a card's static status chip (classed, styled via
    /// CSS). <paramref name="rectSuffix"/>/<paramref name="textSuffix"/> land right before the closing
    /// <c>/&gt;</c>/<c>&gt;</c> (after the textLength guard) and <paramref name="rectPrefix"/>/
    /// <paramref name="textPrefix"/> right after the tag name — exactly where each call site's
    /// class/style attribute sat before the extraction, so both callers stay byte-identical.</summary>
    private static void StatusChip(StringBuilder sb, string text, double x, double sy, double h,
        ITextMeasurer m, BeckStyle style, bool guard,
        string rectPrefix, string rectSuffix, string textPrefix, string textSuffix)
    {
        var spec = style.Typography.Roles.Of(FontRole.Status);
        var sw = m.Measure(text, FontRole.Status, spec).Width;
        var shown = Cased(spec, text);
        sb.Append($"<rect{rectPrefix} x=\"{N(x)}\" y=\"{N(sy)}\" width=\"{N(sw + 16)}\" height=\"{N(h)}\" rx=\"{N(h / 2)}\"{rectSuffix}/>");
        sb.Append($"<text{textPrefix} x=\"{N(x + 8)}\" y=\"{N(sy + h / 2)}\" font-size=\"{N(spec.SizePx)}\" font-weight=\"{P(spec.Weight)}\" dominant-baseline=\"central\" text-anchor=\"start\"{Guard(sw, guard)}{textSuffix}>{SvgWriter.Text(shown)}</text>");
    }

    /// <summary>A status pill — chip bg tinted by the status-pill ratio + coloured text — at (x, sy).</summary>
    private static void StatusPill(StringBuilder sb, string text, string color, double x, double sy, double h, ITextMeasurer m, BeckStyle style, bool guard) =>
        StatusChip(sb, text, x, sy, h, m, style, guard,
            "", $" style=\"fill:color-mix(in srgb,{color} {P(style.Mix.StatusPill)}%,transparent)\"",
            "", $" style=\"fill:{color}\"");

    private static void EmitPill(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard)
    {
        var geo = style.Geometry;
        var bi = geo.NodeBorderInset;
        sb.Append(Artwork.Rect(style, "beck-node beck-node--pill", bi, bi, w - 2 * bi, h - 2 * bi, h / 2, hash + ":" + node.Id, shadow: true));
        sb.Append(Artwork.Scribble(style, bi, bi, w - 2 * bi, h - 2 * bi, h / 2, hash + ":" + node.Id));
        double titleLine = geo.CardTitleLine, subLine = geo.PillSubLine, gap = geo.PillGap;
        var stackH = titleLine + (node.Subtitle != null ? gap + subLine : 0);
        var top = h / 2 - stackH / 2;
        CenterLine(sb, style.Typography.DecorateTitle(node.Title), "beck-node-title", w / 2, top + titleLine / 2, m, style, FontRole.PillTitle, guard);
        if (node.Subtitle is { } sub)
        {
            CenterLine(sb, sub, "beck-node-subtitle", w / 2, top + titleLine + gap + subLine / 2, m, style, FontRole.PillSubtitle, guard);
        }
    }

    private static void EmitClass(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard)
    {
        var geo = style.Geometry;
        var bi = geo.NodeBorderInset;
        var clip = $"cc-{SvgWriter.Attr(node.Id)}-{hash}";
        sb.Append($"<clipPath id=\"{clip}\"><rect x=\"0\" y=\"0\" width=\"{N(w)}\" height=\"{N(h)}\" rx=\"{N(geo.ClassRadius)}\"/></clipPath>");
        sb.Append(Artwork.Rect(style, "beck-node beck-node--class", bi, bi, w - 2 * bi, h - 2 * bi, geo.ClassRadius, hash + ":" + node.Id, shadow: true));
        sb.Append($"<g clip-path=\"url(#{clip})\">");

        double stereoLine = geo.StereoLine, titleLine = geo.ClassTitleLine, headPadY = geo.HeadPadY / 2,
               memberLine = geo.MemberLine, sectionPadY = geo.SectionPadY / 2, memberGap = geo.MemberGap;
        var hasStereo = node.Stereotype != null;
        var headH = (hasStereo ? stereoLine : 0) + titleLine + headPadY * 2 + geo.HeadBorderBottom;
        sb.Append($"<rect class=\"beck-class-head\" x=\"0\" y=\"0\" width=\"{N(w)}\" height=\"{N(headH)}\"/>");
        // Sketch: crayon-scribble the head compartment only (the member lists stay on clean paper).
        sb.Append(Artwork.Scribble(style, bi, bi, w - 2 * bi, headH - bi, geo.ClassRadius, hash + ":" + node.Id));
        var ty = headPadY;
        if (hasStereo)
        {
            CenterLine(sb, $"«{node.Stereotype}»", "beck-class-stereo", w / 2, ty + stereoLine / 2, m, style, FontRole.ClassStereotype, guard);
            ty += stereoLine;
        }
        CenterLine(sb, style.Typography.DecorateTitle(node.Title), "beck-class-title", w / 2, ty + titleLine / 2, m, style, FontRole.ClassTitle, guard);
        sb.Append(ClassSeparator(style, "beck-class-head-border", w, headH, hash + ":" + node.Id + ":hb"));

        var memberSpec = style.Typography.Roles.Of(FontRole.ClassMember);
        var memberX = geo.SectionPadX / 2;
        var y = headH;
        var prior = false;
        foreach (var (members, css) in new[] { (node.Fields, "beck-class-field"), (node.Methods, "beck-class-method") })
        {
            if (members.Count == 0)
            {
                continue;
            }

            if (prior) { sb.Append(ClassSeparator(style, "beck-class-divider", w, y, hash + ":" + node.Id + ":dv" + N(y))); y += geo.HeadBorderBottom; }
            var my = y + sectionPadY;
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

    /// <summary>A diamond node (flowchart decision groundwork): the diamond outline plus a centered
    /// title/subtitle stack wrapped into the inscribed rectangle (<see cref="CardSizer.DiamondTextAvail"/>),
    /// so the drawn wrap matches the box the sizer measured. Icons are skipped (the point geometry leaves
    /// no clean icon gutter); every measured run keeps its <c>textLength</c> guard.</summary>
    private static void EmitDiamond(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard)
    {
        var geo = style.Geometry;
        var bi = geo.NodeBorderInset;
        sb.Append(Artwork.Diamond(style, "beck-node beck-node--diamond", bi, bi, w - 2 * bi, h - 2 * bi, hash + ":" + node.Id, shadow: true));
        EmitCenteredStack(sb, node, w, h, m, style, guard,
            CardSizer.DiamondTextAvail(node, m, geo, style.Typography.Roles, style.Typography.TitlePrefix, style.Typography.TitleSuffix));
    }

    /// <summary>A parallelogram node (flowchart I/O groundwork): the skewed outline plus a centered
    /// title/subtitle stack wrapped into the card text column (<see cref="CardSizer.ParallelogramTextAvail"/>).
    /// The parallelogram is centrally symmetric, so centering at <c>w/2</c> clears both slants.</summary>
    private static void EmitParallelogram(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard)
    {
        var geo = style.Geometry;
        var bi = geo.NodeBorderInset;
        sb.Append(Artwork.Parallelogram(style, "beck-node beck-node--parallelogram", bi, bi, w - 2 * bi, h - 2 * bi, hash + ":" + node.Id, shadow: true));
        EmitCenteredStack(sb, node, w, h, m, style, guard,
            CardSizer.ParallelogramTextAvail(node, m, geo, style.Typography.Roles, style.Typography.TitlePrefix, style.Typography.TitleSuffix));
    }

    /// <summary>The shared centered title/subtitle stack for the diamond/parallelogram shapes: wrap both
    /// runs at <paramref name="avail"/> (the SAME width the sizer used), then center the whole block in the
    /// bbox and each line at <c>w/2</c>. Card-role typography and line metrics; every run carries its guard.</summary>
    private static void EmitCenteredStack(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, BeckStyle style, bool guard, double avail)
    {
        var geo = style.Geometry;
        double titleLine = geo.CardTitleLine, subLine = geo.CardSubLine, textGap = geo.TextGap;
        var titleLines = CardSizer.WrapText(m, style.Typography.DecorateTitle(node.Title), FontRole.CardTitle, avail, style.Typography.Roles);
        var subLines = node.Subtitle != null ? CardSizer.WrapText(m, node.Subtitle, FontRole.CardSubtitle, avail, style.Typography.Roles) : null;
        var textColH = titleLines.Count * titleLine + (subLines != null ? textGap + subLines.Count * subLine : 0);
        var stackY = h / 2 - textColH / 2;
        foreach (var line in titleLines)
        {
            CenterLine(sb, line, "beck-node-title", w / 2, stackY + titleLine / 2, m, style, FontRole.CardTitle, guard);
            stackY += titleLine;
        }

        if (subLines != null)
        {
            stackY += textGap;
            foreach (var line in subLines)
            {
                CenterLine(sb, line, "beck-node-subtitle", w / 2, stackY + subLine / 2, m, style, FontRole.CardSubtitle, guard);
                stackY += subLine;
            }
        }
    }

    /// <summary>A class compartment separator: the straight <c>&lt;line&gt;</c> (classic — byte-identical),
    /// or, when the style sets <see cref="StyleEdges.WobblySeparators"/> (sketch), a subtle wobbly
    /// <c>&lt;path&gt;</c> carrying the same class with its two endpoints preserved and jitter hash-seeded.
    /// The wobble path needs an explicit <c>fill="none"</c> (a <c>&lt;line&gt;</c> is unfillable, so the
    /// shared CSS never sets it).</summary>
    private static string ClassSeparator(BeckStyle style, string cls, double w, double y, string seed)
    {
        if (!style.Edges.WobblySeparators)
        {
            return $"<line class=\"{cls}\" x1=\"0\" y1=\"{N(y)}\" x2=\"{N(w)}\" y2=\"{N(y)}\"/>";
        }

        var amp = Math.Max(style.Edges.BowAmplitude, 2);
        return $"<path class=\"{cls}\" d=\"{Shaping.BowLine(0, y, w, y, amp, seed)}\" fill=\"none\"/>";
    }

    private static void CenterLine(StringBuilder sb, string text, string cls, double cx, double cy, ITextMeasurer m, BeckStyle style, FontRole role, bool guard)
    {
        var spec = style.Typography.Roles.Of(role);
        var tl = m.Measure(text, role, spec).Width;
        sb.Append($"<text class=\"{cls}\" x=\"{N(cx)}\" y=\"{N(cy)}\" font-size=\"{N(spec.SizePx)}\" font-weight=\"{P(spec.Weight)}\" ")
          .Append($"dominant-baseline=\"central\" text-anchor=\"middle\"{Guard(tl, guard)}>")
          .Append(SvgWriter.Text(Cased(spec, text))).Append("</text>");
    }

    private static void EmitCard(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m,
        int idx, string hash, BeckStyle style, bool guard, IReadOnlyList<(string Text, string Color)>? states)
    {
        var geo = style.Geometry;
        var bi = geo.NodeBorderInset;
        var cls = "beck-node";
        if (node.Kind == NodeKind.External)
        {
            cls += " beck-node--external";
        }

        if (node.Variant == NodeVariant.Subtle)
        {
            cls += " beck-node--subtle";
        }

        sb.Append(Artwork.Rect(style, cls, bi, bi, w - 2 * bi, h - 2 * bi, geo.CardRadius, hash + ":" + node.Id, shadow: true));
        sb.Append(Artwork.Scribble(style, bi, bi, w - 2 * bi, h - 2 * bi, geo.CardRadius, hash + ":" + node.Id));

        var hasIcon = Icons.ResolveIcon(node.Icon) != null;
        var padHalf = geo.CardPadX / 2;
        var textX = padHalf + (hasIcon ? geo.IconW + geo.IconGap : 0);
        if (hasIcon)
        {
            var chipY = h / 2 - geo.IconW / 2;
            sb.Append($"<rect class=\"beck-icon-chip\" x=\"{N(padHalf)}\" y=\"{N(chipY)}\" width=\"{N(geo.IconW)}\" height=\"{N(geo.IconW)}\" rx=\"{N(geo.IconChipRadius)}\"/>");
            sb.Append(IconSvg(node.Icon!, padHalf + 7, chipY + 7, 20));
        }

        var statusChipH = geo.StatusChipH;
        double titleLine = geo.CardTitleLine, subLine = geo.CardSubLine, textGap = geo.TextGap;
        double itemLine = geo.ItemLine, itemGap = geo.ItemGap, bodyLine = geo.BodyLine;
        // The same flow-status texts CardSizer sized the box for (row height + widest pill), so
        // wrapping and vertical centering here match the reserved space exactly.
        var stateTexts = NonEmptyStatusTexts(states);
        var hasStates = stateTexts != null;
        // Wrap the title/subtitle into the SAME lines CardSizer measured the box for, so the drawn
        // text stays inside it (a single overflowing <text> was the "text escapes its box" bug).
        var avail = CardSizer.CardTextAvail(node, m, geo, style.Typography.Roles, style.Typography.TitlePrefix, style.Typography.TitleSuffix, stateTexts);
        var titleLines = CardSizer.WrapText(m, style.Typography.DecorateTitle(node.Title), FontRole.CardTitle, avail, style.Typography.Roles);
        var subLines = node.Subtitle != null ? CardSizer.WrapText(m, node.Subtitle, FontRole.CardSubtitle, avail, style.Typography.Roles) : null;
        // Items are single rows (never wrapped); the body wraps into CardTextAvail exactly as CardSizer sized it.
        var hasItems = node.Items.Count > 0;
        var bodyLines = node.Body != null ? CardSizer.WrapText(m, node.Body, FontRole.CardSubtitle, avail, style.Typography.Roles) : null;
        var textColH = titleLines.Count * titleLine
            + (subLines != null ? textGap + subLines.Count * subLine : 0)
            + (hasItems ? textGap + node.Items.Count * itemLine + (node.Items.Count - 1) * itemGap : 0)
            + (bodyLines != null ? textGap + bodyLines.Count * bodyLine : 0)
            + (node.Status != null || hasStates ? textGap + geo.StatusMt + statusChipH : 0);
        var top = h / 2 - textColH / 2;
        var stackY = top;
        foreach (var line in titleLines)
        {
            Line(sb, line, "beck-node-title", textX, stackY + titleLine / 2, m, style, FontRole.CardTitle, guard);
            stackY += titleLine;
        }
        if (subLines != null)
        {
            stackY += textGap;
            foreach (var line in subLines)
            {
                Line(sb, line, "beck-node-subtitle", textX, stackY + subLine / 2, m, style, FontRole.CardSubtitle, guard);
                stackY += subLine;
            }
        }
        if (hasItems)
        {
            stackY += textGap;
            for (var i = 0; i < node.Items.Count; i++)
            {
                if (i > 0)
                {
                    stackY += itemGap;
                }

                // Bullet baked into the measured+drawn run (CardSizer.ItemBullet) so the textLength guard matches.
                Line(sb, CardSizer.ItemBullet + node.Items[i], "beck-node-subtitle", textX, stackY + itemLine / 2, m, style, FontRole.CardSubtitle, guard);
                stackY += itemLine;
            }
        }
        if (bodyLines != null)
        {
            stackY += textGap;
            foreach (var line in bodyLines)
            {
                Line(sb, line, "beck-node-subtitle", textX, stackY + bodyLine / 2, m, style, FontRole.CardSubtitle, guard);
                stackY += bodyLine;
            }
        }
        if (node.Status is { } || states != null)
        {
            var sy = stackY + textGap + geo.StatusMt;
            if (states != null)
            {
                // The flow swaps this pill: pre-build one group per (text,color) state,
                // state 0 (resting, possibly empty) visible, the rest hidden — the
                // compiler cross-fades. A status-less target's box was pre-sized for
                // the row (CardSizer's flowStatuses), so no pill overhangs the card.
                for (var si = 0; si < states.Count; si++)
                {
                    sb.Append($"<g class=\"beck-status-state bss{idx}-{si}-{hash}\"{(si == 0 ? "" : " opacity=\"0\"")}>");
                    if (states[si].Text.Length > 0)
                    {
                        StatusPill(sb, states[si].Text, states[si].Color, textX, sy, statusChipH, m, style, guard);
                    }

                    sb.Append("</g>");
                }
            }
            else
            {
                StatusChip(sb, node.Status!, textX, sy, statusChipH, m, style, guard,
                    " class=\"beck-status-bg\"", "", " class=\"beck-status-text\"", "");
            }
        }
    }

    private static void EmitGhost(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard)
    {
        var geo = style.Geometry;
        var bi = geo.NodeBorderInset;
        sb.Append(Artwork.Rect(style, "beck-node beck-node--ghost", bi, bi, w - 2 * bi, h - 2 * bi, geo.GhostRadius, hash + ":" + node.Id));
        var hasIcon = Icons.ResolveIcon(node.Icon) != null;
        var rowH = Math.Max(hasIcon ? geo.GhostIcon : 0, geo.GhostLabelLine);
        var rowTop = h / 2 - (node.Status != null ? (rowH + geo.GhostGap + geo.StatusInlineLine) / 2 : rowH / 2);
        var ghostPadHalf = geo.GhostPadX / 2;
        var labelX = ghostPadHalf + (hasIcon ? geo.GhostIcon + geo.GhostIconGap : 0);
        if (hasIcon)
        {
            sb.Append(IconSvg(node.Icon!, ghostPadHalf, rowTop + rowH / 2 - 7, 14));
        }

        Line(sb, style.Typography.DecorateTitle(node.Title), "beck-ghost-label", labelX, rowTop + rowH / 2, m, style, FontRole.GhostLabel, guard);
        if (node.Status is { } status)
        {
            var statusY = rowTop + rowH + geo.GhostGap;
            Line(sb, status, "beck-status-inline", ghostPadHalf, statusY + geo.StatusInlineLine / 2, m, style, FontRole.StatusInline, guard);
        }
    }

    /// <summary>Render a mindmap node by its depth role (handoff "Branch accents"): a leaf pill, or a
    /// root/rank-1 heading card. Returns false for a content card (items/body at rank 2+) so the caller
    /// falls back to the shared card emitter. Ghost branches route here too (dashed, neutral, "planned").</summary>
    private static bool EmitMindMapNode(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard)
    {
        var ghost = node.Variant == NodeVariant.Ghost || node.Kind == NodeKind.Ghost;
        if (node.Shape == NodeShape.Pill)
        {
            EmitMindMapLeaf(sb, node, w, h, m, hash, style, guard, ghost);
            return true;
        }

        var rank = (int)(node.Rank ?? 0);
        var heading = node.Shape == NodeShape.Card && node.Items.Count == 0 && node.Body == null && rank <= 1;
        if (ghost || heading)
        {
            EmitMindMapCard(sb, node, w, h, m, hash, style, guard, rank, ghost);
            return true;
        }

        return false;
    }

    /// <summary>A mindmap leaf pill: accent-tinted fill + hairline accent border (<c>.beck-mm-leaf</c>), no
    /// shadow, with an Inter 12/500 label left-aligned at 16px (node internals stay LTR on both sides).
    /// Ghost leaves render the shared dashed transparent treatment with a muted label.</summary>
    private static void EmitMindMapLeaf(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard, bool ghost)
    {
        var bi = style.Geometry.NodeBorderInset;
        var cls = ghost ? "beck-node beck-node--ghost" : "beck-node beck-node--pill beck-mm-leaf";
        sb.Append(Artwork.Rect(style, cls, bi, bi, w - 2 * bi, h - 2 * bi, h / 2, hash + ":" + node.Id, shadow: false));
        LineSpec(sb, style.Typography.DecorateTitle(node.Title), ghost ? "beck-ghost-label" : "beck-node-title",
            CardSizer.MindMapLeafPadX / 2, h / 2, m, CardSizer.MindMapLeafLabel, guard);
    }

    /// <summary>A mindmap root/rank-1 heading card: accent-bordered box (dashed + shadowless when ghost),
    /// an optional icon chip (34 at root, 30 at rank 1), and a centred stack of title (+ optional subtitle)
    /// (+ a semantic status pill, or a faint "planned" label on a ghost branch). The box was floored to hold
    /// the single-line heading, so the title never wraps here.</summary>
    private static void EmitMindMapCard(StringBuilder sb, NodeModel node, double w, double h, ITextMeasurer m, string hash, BeckStyle style, bool guard, int rank, bool ghost)
    {
        var geo = style.Geometry;
        var bi = geo.NodeBorderInset;
        var radius = rank == 0 ? geo.CardRadius : 12;
        sb.Append(Artwork.Rect(style, ghost ? "beck-node beck-node--ghost" : "beck-node",
            bi, bi, w - 2 * bi, h - 2 * bi, radius, hash + ":" + node.Id, shadow: !ghost));

        var hasIcon = Icons.ResolveIcon(node.Icon) != null;
        var chipW = rank == 0 ? CardSizer.MindMapRootChip : CardSizer.MindMapRankChip;
        var iconSize = rank == 0 ? 20.0 : 18.0;
        var padHalf = geo.CardPadX / 2;
        var textX = padHalf + (hasIcon ? chipW + geo.IconGap : 0);
        if (hasIcon)
        {
            var chipY = h / 2 - chipW / 2;
            sb.Append($"<rect class=\"beck-icon-chip\" x=\"{N(padHalf)}\" y=\"{N(chipY)}\" width=\"{N(chipW)}\" height=\"{N(chipW)}\" rx=\"{N(geo.IconChipRadius)}\"/>");
            sb.Append(IconSvg(node.Icon!, padHalf + (chipW - iconSize) / 2, chipY + (chipW - iconSize) / 2, iconSize));
        }

        double titleLine = geo.CardTitleLine, subLine = geo.CardSubLine, gap = geo.TextGap;
        var hasSub = node.Subtitle != null;
        var showStatus = !ghost && node.Status != null;
        var statusChipH = geo.StatusChipH;
        var plannedLine = geo.StatusInlineLine;
        var stackH = titleLine
            + (hasSub ? gap + subLine : 0)
            + (showStatus ? gap + statusChipH : 0)
            + (ghost ? gap + plannedLine : 0);
        var y = h / 2 - stackH / 2;

        Line(sb, style.Typography.DecorateTitle(node.Title), ghost ? "beck-node-subtitle" : "beck-node-title",
            textX, y + titleLine / 2, m, style, FontRole.CardTitle, guard);
        y += titleLine;
        if (hasSub)
        {
            y += gap;
            Line(sb, node.Subtitle!, "beck-node-subtitle", textX, y + subLine / 2, m, style, FontRole.CardSubtitle, guard);
            y += subLine;
        }

        if (showStatus)
        {
            StatusPill(sb, node.Status!, MindMapStatusColor(node.Status!), textX, y + gap, statusChipH, m, style, guard);
        }
        else if (ghost)
        {
            Line(sb, node.Status ?? "planned", "beck-mm-planned", textX, y + gap + plannedLine / 2, m, style, FontRole.StatusInline, guard);
        }
    }

    /// <summary>The semantic pill colour for a mindmap status keyword (handoff): the status carries its own
    /// meaning independent of the branch accent. Unknown keywords fall back to the branch accent.</summary>
    private static string MindMapStatusColor(string status) => status.Trim().ToLowerInvariant() switch
    {
        "complete" or "done" or "shipped" or "live" => "var(--beck-success)",
        "in progress" or "in-progress" or "active" or "wip" or "building" => "var(--beck-warn)",
        "blocked" or "failed" or "at risk" or "at-risk" => "var(--beck-danger)",
        "review" or "in review" or "in-review" => "var(--beck-info)",
        "planned" or "backlog" or "later" or "todo" => "var(--beck-neutral)",
        _ => "var(--beck-accent)",
    };

    /// <summary>A left-aligned single-line text at an explicit <see cref="FontRoleSpec"/> — used for the
    /// mindmap leaf label, whose Inter 12/500 type isn't a style role. Measured through a sans role so a
    /// custom measurer still honours the spec; the textLength guard pins the drawn advance to it.</summary>
    private static void LineSpec(StringBuilder sb, string text, string cls, double x, double cy, ITextMeasurer m, FontRoleSpec spec, bool guard)
    {
        var tl = m.Measure(text, FontRole.PillTitle, spec).Width;
        sb.Append($"<text class=\"{cls}\" x=\"{N(x)}\" y=\"{N(cy)}\" font-size=\"{N(spec.SizePx)}\" font-weight=\"{P(spec.Weight)}\" ")
          .Append($"dominant-baseline=\"central\" text-anchor=\"start\"{Guard(tl, guard)}>")
          .Append(SvgWriter.Text(text)).Append("</text>");
    }

    private static void Line(StringBuilder sb, string text, string cls, double x, double cy, ITextMeasurer m, BeckStyle style, FontRole role, bool guard)
    {
        var spec = style.Typography.Roles.Of(role);
        var tl = m.Measure(text, role, spec).Width;
        sb.Append($"<text class=\"{cls}\" x=\"{N(x)}\" y=\"{N(cy)}\" font-size=\"{N(spec.SizePx)}\" font-weight=\"{P(spec.Weight)}\" ")
          .Append($"dominant-baseline=\"central\" text-anchor=\"start\"{Guard(tl, guard)}>")
          .Append(SvgWriter.Text(Cased(spec, text))).Append("</text>");
    }

    private static string IconSvg(string icon, double x, double y, double size)
    {
        var body = Icons.ResolveIcon(icon);
        if (body is null)
        {
            return "";
        }
        // Position the resolved <svg ...> and tint it via the accent (currentColor).
        return body.Replace("<svg ",
            $"<svg class=\"beck-icon\" x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(size)}\" height=\"{N(size)}\" style=\"color:var(--beck-accent)\" ", StringComparison.Ordinal);
    }
}