using System.Globalization;
using System.Text;
using Beck.Model;
using Beck.Text;

namespace Beck.Svg;

/// <summary>
/// Draws a <c>type: chart</c> chart directly to SVG — bar, line, pie/donut, or scatter, plus an
/// optional legend. Charts bypass the node/edge layout+route+animate pipeline entirely: this painter
/// returns the body markup and its natural size, which <see cref="SvgRenderer"/> drops into the shared
/// <c>&lt;svg&gt;</c> shell (title block + <c>--beck-*</c> token <c>&lt;style&gt;</c>). Every colour is
/// a <c>var(--beck-*)</c> token or a <see cref="ChartColors"/> expression over <c>--beck-primary</c>,
/// so a chart adopts the host palette and flips light↔dark like the rest of Beck. Static — no motion.
/// </summary>
internal static class ChartPainter
{
    private const double Pad = 16;      // outer margin around the whole chart
    private const double Gap = 24;      // between plot and legend
    private const double PlotW = 400, PlotH = 250, PieSize = 260;

    private static string N(double n) => SvgWriter.Num(n);

    /// <summary>Sanitise a datum to a finite number — NaN/±∞ (e.g. from an overflowing literal like
    /// <c>1e400</c>) collapse to 0, so no non-finite coordinate can ever reach the SVG.</summary>
    private static double Fin(double v) => double.IsFinite(v) ? v : 0;

    /// <summary>Format a value label: plain for the ordinary range, compact exponential for very large
    /// or very small magnitudes so one extreme datum can't blow up the legend (and canvas) width.</summary>
    private static string Fmt(double v)
    {
        if (!double.IsFinite(v))
        {
            return "0";
        }

        var a = Math.Abs(v);
        return a != 0 && (a >= 1e6 || a < 1e-3)
            ? v.ToString("0.##e+0", CultureInfo.InvariantCulture)
            : v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Render the chart body and report its natural (unpadded content + outer <see cref="Pad"/>) size.</summary>
    public static (string Markup, double Width, double Height) Render(ChartModel chart, ITextMeasurer m, bool guard)
    {
        var n = chart.Series.Count;
        var palette = ChartColors.Palette(chart.Palette, n);
        var colors = chart.Series.Select((s, i) => s.Color ?? palette[i]).ToList();

        var pieish = chart.Kind is ChartKind.Pie or ChartKind.Donut;
        double plotW = pieish ? PieSize : PlotW, plotH = pieish ? PieSize : PlotH;
        var plot = chart.Kind switch
        {
            ChartKind.Bar => Bar(chart, colors, plotW, plotH, guard, m),
            ChartKind.Line => Line(chart, colors, plotW, plotH),
            ChartKind.Scatter => Scatter(chart, colors, plotW, plotH),
            _ => Pie(chart, colors, plotW, plotH, chart.Kind == ChartKind.Donut, guard, m),
        };

        // Legend geometry + the block layout around the plot.
        var legend = chart.Legend == LegendPlacement.None ? null : BuildLegend(chart, colors, m, guard, plotW);
        double contentW, contentH, plotX, plotY;
        var legendGroup = "";

        if (legend is not { } lg)
        {
            contentW = plotW;
            contentH = plotH;
            plotX = 0;
            plotY = 0;
        }
        else if (chart.Legend == LegendPlacement.Right)
        {
            contentW = plotW + Gap + lg.Width;
            contentH = Math.Max(plotH, lg.Height);
            plotX = 0;
            plotY = (contentH - plotH) / 2;
            legendGroup = Translate(plotW + Gap, (contentH - lg.Height) / 2, lg.Markup);
        }
        else // Top or Bottom (horizontal band; widened past the plot only if a long label overflows)
        {
            contentW = lg.Width;
            contentH = plotH + Gap + lg.Height;
            plotX = (lg.Width - plotW) / 2;
            var top = chart.Legend == LegendPlacement.Top;
            plotY = top ? lg.Height + Gap : 0;
            legendGroup = Translate(0, top ? 0 : plotH + Gap, lg.Markup);
        }

        var body = new StringBuilder();
        body.Append("<g class=\"beck-chart\" transform=\"translate(").Append(N(Pad)).Append(',').Append(N(Pad)).Append(")\">");
        body.Append(Translate(plotX, plotY, plot));
        body.Append(legendGroup);
        body.Append("</g>");
        return (body.ToString(), contentW + 2 * Pad, contentH + 2 * Pad);
    }

    private static string Translate(double dx, double dy, string inner) =>
        inner.Length == 0 ? "" : $"<g transform=\"translate({N(dx)},{N(dy)})\">{inner}</g>";

    // ---- plots ----

    private static string Bar(ChartModel chart, IReadOnlyList<string> colors, double w, double h, bool guard, ITextMeasurer m)
    {
        double t = 18, r = 10, b = 12, l = 10;
        double pw = w - l - r, ph = h - t - b;
        var vals = chart.Series.Select(s => Fin(s.Values.Count > 0 ? s.Values[0] : 0)).ToList();
        var max = vals.DefaultIfEmpty(0).Max();
        if (max <= 0)
        {
            max = 1;
        }

        var gap = pw / vals.Count;
        var bw = Math.Min(gap * 0.62, 64);
        var baseY = t + ph;
        var spec = FontRoles.Of(FontRole.Status);

        var sb = new StringBuilder();
        sb.Append($"<line x1=\"{N(l)}\" y1=\"{N(baseY)}\" x2=\"{N(w - r)}\" y2=\"{N(baseY)}\" style=\"stroke:var(--beck-edge);stroke-width:1\"/>");
        for (var i = 0; i < vals.Count; i++)
        {
            // Clamp the bar into the plot: a negative datum reads as an empty bar, a huge one as a
            // full-height bar — never a negative rect height or an off-canvas top.
            var bh = Math.Clamp(vals[i] / max * ph, 0, ph);
            var x = l + gap * i + (gap - bw) / 2;
            var y = baseY - bh;
            sb.Append($"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(bw)}\" height=\"{N(bh)}\" rx=\"4\" style=\"fill:{SvgWriter.Attr(colors[i])}\"/>");
            var label = Fmt(vals[i]);
            var lw = m.Measure(label, FontRole.Status, spec).Width;
            sb.Append($"<text x=\"{N(x + bw / 2)}\" y=\"{N(y - 6)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
              .Append($"font-size=\"{N(spec.SizePx)}\" style=\"fill:var(--beck-text-muted);font-family:var(--beck-font-mono)\"{SvgRenderer.Guard(lw, guard)}>{SvgWriter.Text(label)}</text>");
        }

        return sb.ToString();
    }

    private static string Line(ChartModel chart, IReadOnlyList<string> colors, double w, double h)
    {
        double t = 14, r = 14, b = 16, l = 16;
        double pw = w - l - r, ph = h - t - b;
        var cols = chart.Series.Max(s => s.Values.Count);
        var denom = Math.Max(cols - 1, 1);
        var max = chart.Series.SelectMany(s => s.Values).Select(Fin).DefaultIfEmpty(0).Max();
        if (max <= 0)
        {
            max = 1;
        }

        double X(int i) => l + pw / denom * i;
        double Y(double v) => Math.Clamp(t + ph - Fin(v) / max * ph, t, t + ph);

        var sb = new StringBuilder();
        sb.Append(Grid(w, l, r, t, ph));
        for (var si = 0; si < chart.Series.Count; si++)
        {
            var s = chart.Series[si];
            if (s.Values.Count == 0)
            {
                continue;
            }

            var pts = string.Join(" ", s.Values.Select((v, i) => $"{N(X(i))},{N(Y(v))}"));
            sb.Append($"<polyline points=\"{pts}\" style=\"fill:none;stroke:{SvgWriter.Attr(colors[si])};stroke-width:2;stroke-linejoin:round;stroke-linecap:round\"/>");
            var last = s.Values.Count - 1;
            sb.Append($"<circle cx=\"{N(X(last))}\" cy=\"{N(Y(s.Values[last]))}\" r=\"3\" style=\"fill:{SvgWriter.Attr(colors[si])}\"/>");
        }

        return sb.ToString();
    }

    private static string Scatter(ChartModel chart, IReadOnlyList<string> colors, double w, double h)
    {
        double t = 14, r = 14, b = 14, l = 16;
        double pw = w - l - r, ph = h - t - b;
        var flat = chart.Series.SelectMany(s => s.Points).ToList();
        var mx = flat.Select(p => Fin(p.X)).DefaultIfEmpty(1).Max();
        var my = flat.Select(p => Fin(p.Y)).DefaultIfEmpty(1).Max();
        if (mx <= 0)
        {
            mx = 1;
        }

        if (my <= 0)
        {
            my = 1;
        }

        // Clamp each point into the plot rect: an out-of-range (negative or huge) coordinate lands on
        // the border rather than off-canvas.
        double X(double x) => Math.Clamp(l + Fin(x) / (mx * 1.05) * pw, l, w - r);
        double Y(double y) => Math.Clamp(t + ph - Fin(y) / (my * 1.05) * ph, t, t + ph);

        var sb = new StringBuilder();
        sb.Append(Grid(w, l, r, t, ph));
        for (var si = 0; si < chart.Series.Count; si++)
        {
            foreach (var p in chart.Series[si].Points)
            {
                sb.Append($"<circle cx=\"{N(X(p.X))}\" cy=\"{N(Y(p.Y))}\" r=\"5\" style=\"fill:{SvgWriter.Attr(colors[si])};fill-opacity:0.82\"/>");
            }
        }

        return sb.ToString();
    }

    private static string Pie(ChartModel chart, IReadOnlyList<string> colors, double w, double h, bool donut, bool guard, ITextMeasurer m)
    {
        double cx = w / 2, cy = h / 2;
        var rOuter = Math.Min(w, h) / 2 - 6;
        var rInner = donut ? rOuter * 0.58 : 0;
        // A slice can only be a non-negative magnitude — negatives and non-finite data collapse to 0
        // so no wedge sweeps a backwards or non-finite angle.
        var vals = chart.Series.Select(s => Math.Max(0, Fin(s.Values.Count > 0 ? s.Values[0] : 0))).ToList();
        var total = vals.Sum();
        if (total <= 0)
        {
            total = 1;
        }

        var sb = new StringBuilder();
        // A single slice is a full ring/disc — the arc would be zero-length (start == end), so draw circles.
        if (vals.Count == 1)
        {
            sb.Append($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(rOuter)}\" style=\"fill:{SvgWriter.Attr(colors[0])}\"/>");
            if (donut)
            {
                sb.Append($"<circle cx=\"{N(cx)}\" cy=\"{N(cy)}\" r=\"{N(rInner)}\" style=\"fill:var(--beck-node-bg)\"/>");
            }
        }
        else
        {
            var ang = -Math.PI / 2;
            for (var i = 0; i < vals.Count; i++)
            {
                double a0 = ang, a1 = ang + vals[i] / total * Math.PI * 2;
                ang = a1;
                var (x0, y0) = Pol(cx, cy, rOuter, a0);
                var (x1, y1) = Pol(cx, cy, rOuter, a1);
                var large = a1 - a0 > Math.PI ? 1 : 0;
                string d;
                if (donut)
                {
                    var (xi1, yi1) = Pol(cx, cy, rInner, a1);
                    var (xi0, yi0) = Pol(cx, cy, rInner, a0);
                    d = $"M{N(x0)} {N(y0)} A{N(rOuter)} {N(rOuter)} 0 {large} 1 {N(x1)} {N(y1)} L{N(xi1)} {N(yi1)} A{N(rInner)} {N(rInner)} 0 {large} 0 {N(xi0)} {N(yi0)} Z";
                }
                else
                {
                    d = $"M{N(cx)} {N(cy)} L{N(x0)} {N(y0)} A{N(rOuter)} {N(rOuter)} 0 {large} 1 {N(x1)} {N(y1)} Z";
                }

                sb.Append($"<path d=\"{d}\" style=\"fill:{SvgWriter.Attr(colors[i])};stroke:var(--beck-node-bg);stroke-width:2\"/>");
            }
        }

        if (chart.Center is { } center)
        {
            var hasSub = chart.CenterLabel != null;
            var cSpec = FontRoles.Of(FontRole.DiagramTitle);
            var cw = m.Measure(center, FontRole.DiagramTitle, cSpec).Width;
            sb.Append($"<text x=\"{N(cx)}\" y=\"{N(cy + (hasSub ? -2 : 4))}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
              .Append($"font-size=\"18\" font-weight=\"700\" style=\"fill:var(--beck-text)\"{SvgRenderer.Guard(cw, guard)}>{SvgWriter.Text(center)}</text>");
            if (chart.CenterLabel is { } sub)
            {
                sb.Append($"<text x=\"{N(cx)}\" y=\"{N(cy + 16)}\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
                  .Append($"font-size=\"9\" letter-spacing=\"0.06em\" style=\"fill:var(--beck-text-faint)\">{SvgWriter.Text(sub.ToUpperInvariant())}</text>");
            }
        }

        return sb.ToString();
    }

    /// <summary>Four evenly-spaced horizontal gridlines (top through baseline), for line/scatter plots.</summary>
    private static string Grid(double w, double l, double r, double t, double ph)
    {
        var sb = new StringBuilder();
        for (var g = 0; g <= 3; g++)
        {
            var y = t + ph / 3 * g;
            sb.Append($"<line x1=\"{N(l)}\" y1=\"{N(y)}\" x2=\"{N(w - r)}\" y2=\"{N(y)}\" style=\"stroke:var(--beck-edge);stroke-width:1;opacity:0.55\"/>");
        }

        return sb.ToString();
    }

    private static (double X, double Y) Pol(double cx, double cy, double r, double a) =>
        (cx + r * Math.Cos(a), cy + r * Math.Sin(a));

    // ---- legend ----

    private sealed record Legend(string Markup, double Width, double Height);

    private static Legend BuildLegend(ChartModel chart, IReadOnlyList<string> colors, ITextMeasurer m, bool guard, double plotW)
    {
        var labelSpec = FontRoles.Of(FontRole.CardSubtitle);
        var valueSpec = FontRoles.Of(FontRole.MsgText);
        const double Sw = 11, SwGap = 8, RowH = 22;

        // Inline values only make sense for a single-magnitude series (bar/pie/donut) in a column legend.
        var showValues = chart.LegendValues && chart.Legend == LegendPlacement.Right
            && chart.Series.All(s => s.Values.Count == 1);

        var labels = chart.Series.Select(s => s.Label).ToList();
        var labelWs = labels.Select(t => m.Measure(t, FontRole.CardSubtitle, labelSpec).Width).ToList();

        string Swatch(double y, string color) =>
            $"<rect x=\"0\" y=\"{N(y + (RowH - Sw) / 2)}\" width=\"{N(Sw)}\" height=\"{N(Sw)}\" rx=\"3\" style=\"fill:{SvgWriter.Attr(color)}\"/>";

        string LabelText(double x, double y, string text, double tw) =>
            $"<text x=\"{N(x)}\" y=\"{N(y + RowH / 2)}\" dominant-baseline=\"central\" text-anchor=\"start\" " +
            $"font-size=\"{N(labelSpec.SizePx)}\" style=\"fill:var(--beck-text-muted)\"{SvgRenderer.Guard(tw, guard)}>{SvgWriter.Text(text)}</text>";

        if (chart.Legend == LegendPlacement.Right)
        {
            var maxLabel = labelWs.DefaultIfEmpty(0).Max();
            var textX = Sw + SwGap;
            double valueColW = 0, valueX = 0;
            List<string>? valueStrs = null;
            if (showValues)
            {
                valueStrs = chart.Series.Select(s => Fmt(Fin(s.Values[0]))).ToList();
                valueColW = valueStrs.Select(v => m.Measure(v, FontRole.MsgText, valueSpec).Width).DefaultIfEmpty(0).Max();
                valueX = textX + maxLabel + 16 + valueColW;
            }

            var colW = textX + maxLabel + (showValues ? 16 + valueColW : 0);
            var sb = new StringBuilder();
            for (var i = 0; i < labels.Count; i++)
            {
                var y = i * RowH;
                sb.Append(Swatch(y, colors[i]));
                sb.Append(LabelText(textX, y, labels[i], labelWs[i]));
                if (showValues && valueStrs != null)
                {
                    var vw = m.Measure(valueStrs[i], FontRole.MsgText, valueSpec).Width;
                    sb.Append($"<text x=\"{N(valueX)}\" y=\"{N(y + RowH / 2)}\" dominant-baseline=\"central\" text-anchor=\"end\" ")
                      .Append($"font-size=\"{N(valueSpec.SizePx)}\" style=\"fill:var(--beck-text-faint);font-family:var(--beck-font-mono)\"{SvgRenderer.Guard(vw, guard)}>{SvgWriter.Text(valueStrs[i])}</text>");
                }
            }

            return new Legend(sb.ToString(), colW, labels.Count * RowH);
        }

        // Top / bottom: a centered, wrapping row of swatch+label chips within the plot width.
        const double EntryGap = 18;
        var entryWs = labelWs.Select(lw => Sw + SwGap + lw).ToList();
        var rows = new List<List<int>>();
        var cur = new List<int>();
        double curW = 0;
        for (var i = 0; i < entryWs.Count; i++)
        {
            var add = cur.Count == 0 ? entryWs[i] : EntryGap + entryWs[i];
            if (cur.Count > 0 && curW + add > plotW)
            {
                rows.Add(cur);
                cur = new List<int>();
                curW = 0;
                add = entryWs[i];
            }

            cur.Add(i);
            curW += add;
        }

        if (cur.Count > 0)
        {
            rows.Add(cur);
        }

        // A single entry can be wider than the plot (a very long label can't wrap); widen the whole
        // block to the widest row so every row still centres on-canvas.
        double RowWidth(List<int> row) => row.Sum(i => entryWs[i]) + EntryGap * (row.Count - 1);
        var legendW = Math.Max(plotW, rows.Count == 0 ? 0 : rows.Max(RowWidth));
        var markup = RowLegend(rows, entryWs, labels, labelWs, colors, guard, legendW, Sw, SwGap, RowH, EntryGap, labelSpec.SizePx);
        return new Legend(markup, legendW, rows.Count * RowH);
    }

    /// <summary>Emit the wrapping row legend with each entry placed at its absolute x (swatch + label together).</summary>
    private static string RowLegend(List<List<int>> rows, List<double> entryWs, List<string> labels, List<double> labelWs,
        IReadOnlyList<string> colors, bool guard, double plotW, double sw, double swGap, double rowH, double entryGap, double labelSize)
    {
        var sb = new StringBuilder();
        for (var rIdx = 0; rIdx < rows.Count; rIdx++)
        {
            var row = rows[rIdx];
            var rowW = row.Sum(i => entryWs[i]) + entryGap * (row.Count - 1);
            var x = (plotW - rowW) / 2;
            var y = rIdx * rowH;
            foreach (var i in row)
            {
                sb.Append($"<rect x=\"{N(x)}\" y=\"{N(y + (rowH - sw) / 2)}\" width=\"{N(sw)}\" height=\"{N(sw)}\" rx=\"3\" style=\"fill:{SvgWriter.Attr(colors[i])}\"/>");
                sb.Append($"<text x=\"{N(x + sw + swGap)}\" y=\"{N(y + rowH / 2)}\" dominant-baseline=\"central\" text-anchor=\"start\" ")
                  .Append($"font-size=\"{N(labelSize)}\" style=\"fill:var(--beck-text-muted)\"{SvgRenderer.Guard(labelWs[i], guard)}>{SvgWriter.Text(labels[i])}</text>");
                x += entryWs[i] + entryGap;
            }
        }

        return sb.ToString();
    }
}
