using Beck.Rendering.Route;
using Beck.Rendering.Text;

namespace Beck.Rendering.Svg;

/// <summary>
/// Places edge labels + end multiplicities — a port of the label half of
/// <c>src/route/svg.ts</c> (chooseLabelBox, seg/box gap, drawLabel, drawEndLabel),
/// with <c>getBBox</c> replaced by the measurer at the edge-label role.
/// </summary>
internal sealed class LabelPlacer
{
    private const double LabelGap = 8, LabelPadX = 4, LabelPadY = 2, LabelMargin = 4, LabelEndInset = 14;

    private sealed record Box(double Cx, double Cy, double Hw, double Hh, string Anchor);

    private readonly List<Rect> _obstacles;
    private readonly double _w, _h;
    private readonly List<Rect> _placed = new();

    public LabelPlacer(IEnumerable<Rect> nodeRects, double width, double height)
    {
        _obstacles = nodeRects.ToList();
        _w = width;
        _h = height;
    }

    private static string N(double n) => SvgWriter.Num(n);
    private static string I(double n) => Js.Str(Js.Round(n));

    public string MidLabel(IReadOnlyList<Point> points, string text, List<IReadOnlyList<Point>> otherLines, ITextMeasurer m)
    {
        double w = m.Measure(text, FontRole.EdgeLabel).Width;
        if (w <= 0) w = text.Length * 7;
        double h = 11.2; // edge-label ink height ≈ font size
        double hw = w / 2 + LabelPadX, hh = h / 2 + LabelPadY;
        List<Rect> all = _placed.Count > 0 ? _obstacles.Concat(_placed).ToList() : _obstacles;
        Box box = ChooseLabelBox(points, hw, hh, all, otherLines, PolylineMidpoint(points));
        double tx = box.Anchor == "start" ? box.Cx - w / 2 : box.Anchor == "end" ? box.Cx + w / 2 : box.Cx;
        _placed.Add(new Rect(box.Cx - box.Hw, box.Cy - box.Hh, box.Hw * 2, box.Hh * 2));
        return $"<text class=\"beck-edge-label\" x=\"{I(tx)}\" y=\"{I(box.Cy)}\" text-anchor=\"{box.Anchor}\" dominant-baseline=\"central\" font-size=\"11.2\" font-weight=\"500\" textLength=\"{N(w)}\" lengthAdjust=\"spacingAndGlyphs\">{SvgWriter.Text(text)}</text>";
    }

    public string EndLabel(IReadOnlyList<Point> points, string text, bool atStart, ITextMeasurer m)
    {
        if (points.Count < 2) return "";
        Point a = atStart ? points[0] : points[^1];
        Point b = atStart ? points[1] : points[^2];
        double len = StepRound.Dist(a, b);
        if (len == 0) len = 1;
        double dx = (b.X - a.X) / len, dy = (b.Y - a.Y) / len;
        double px = a.X + dx * 18 - dy * 10, py = a.Y + dy * 18 + dx * 10;
        double w = m.Measure(text, FontRole.EdgeLabel).Width;
        double hw = text.Length * 3.5 + 3;
        _placed.Add(new Rect(px - hw, py - 7, hw * 2, 14));
        return $"<text class=\"beck-edge-label\" x=\"{I(px)}\" y=\"{I(py)}\" text-anchor=\"middle\" dominant-baseline=\"central\" font-size=\"11.2\" font-weight=\"500\" textLength=\"{N(w)}\" lengthAdjust=\"spacingAndGlyphs\">{SvgWriter.Text(text)}</text>";
    }

    private double BoxGap(Box box, Rect r)
    {
        double ix = Math.Min(box.Cx + box.Hw, r.X + r.W) - Math.Max(box.Cx - box.Hw, r.X);
        double iy = Math.Min(box.Cy + box.Hh, r.Y + r.H) - Math.Max(box.Cy - box.Hh, r.Y);
        if (ix > 0 && iy > 0) return -Math.Min(ix, iy);
        return Math.Sqrt((ix > 0 ? 0 : ix * ix) + (iy > 0 ? 0 : iy * iy));
    }

    private double SegGap(Box box, Point a, Point b)
    {
        double x1 = box.Cx - box.Hw, y1 = box.Cy - box.Hh, x2 = box.Cx + box.Hw, y2 = box.Cy + box.Hh;
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double t0 = 0, t1 = 1;
        bool inside = true;
        foreach (var (p, q) in new[] { (-dx, a.X - x1), (dx, x2 - a.X), (-dy, a.Y - y1), (dy, y2 - a.Y) })
        {
            if (p == 0) { if (q < 0) { inside = false; break; } }
            else
            {
                double rr = q / p;
                if (p < 0) t0 = Math.Max(t0, rr); else t1 = Math.Min(t1, rr);
                if (t0 > t1) { inside = false; break; }
            }
        }
        if (inside) return -4;
        double len2 = dx * dx + dy * dy; if (len2 == 0) len2 = 1;
        double min = double.PositiveInfinity;
        foreach (var (cxp, cyp) in new[] { (x1, y1), (x2, y1), (x1, y2), (x2, y2) })
        {
            double t = Math.Max(0, Math.Min(1, ((cxp - a.X) * dx + (cyp - a.Y) * dy) / len2));
            min = Math.Min(min, StepRound.Dist(new Point(cxp, cyp), new Point(a.X + dx * t, a.Y + dy * t)));
        }
        return min;
    }

    private double Clearance(Box box, List<Rect> obstacles, List<IReadOnlyList<Point>> lines)
    {
        double min = double.PositiveInfinity;
        foreach (var o in obstacles) min = Math.Min(min, BoxGap(box, o));
        foreach (var poly in lines)
            for (int i = 0; i < poly.Count - 1; i++) min = Math.Min(min, SegGap(box, poly[i], poly[i + 1]));
        return min;
    }

    private static Point PolylineMidpoint(IReadOnlyList<Point> points)
    {
        var segs = new List<double>();
        double total = 0;
        for (int i = 0; i < points.Count - 1; i++) { double l = StepRound.Dist(points[i], points[i + 1]); segs.Add(l); total += l; }
        double half = total / 2;
        for (int i = 0; i < segs.Count; i++)
        {
            if (half <= segs[i])
            {
                double t = segs[i] != 0 ? half / segs[i] : 0;
                return new Point(points[i].X + (points[i + 1].X - points[i].X) * t, points[i].Y + (points[i + 1].Y - points[i].Y) * t);
            }
            half -= segs[i];
        }
        return points.Count > 0 ? points[^1] : new Point(0, 0);
    }

    private Box ChooseLabelBox(IReadOnlyList<Point> points, double hw, double hh, List<Rect> obstacles, List<IReadOnlyList<Point>> lines, Point mid)
    {
        (double cx, double cy) Clamp(double cx, double cy) => (
            Math.Min(Math.Max(cx, LabelMargin + hw), Math.Max(LabelMargin + hw, _w - LabelMargin - hw)),
            Math.Min(Math.Max(cy, LabelMargin + hh), Math.Max(LabelMargin + hh, _h - LabelMargin - hh)));

        Box? best = null;
        double bestClear = double.NegativeInfinity, bestDist = double.PositiveInfinity;
        void Consider(double cx0, double cy0, string anchor, int segIdx)
        {
            var (cx, cy) = Clamp(cx0, cy0);
            var box = new Box(cx, cy, hw, hh, anchor);
            double clear = Clearance(box, obstacles, lines);
            for (int j = 0; j < points.Count - 1; j++) { if (j == segIdx) continue; clear = Math.Min(clear, SegGap(box, points[j], points[j + 1])); }
            double dist = StepRound.Dist(new Point(cx, cy), mid);
            bool tie = clear == bestClear || Math.Abs(clear - bestClear) <= 6;
            if (best is null || clear > bestClear + 6 || (tie && dist < bestDist)) { best = box; bestClear = clear; bestDist = dist; }
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            Point a = points[i], b = points[i + 1];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) continue;
            double inset = Math.Min(LabelEndInset / len, 0.5);
            int steps = Math.Max(1, Math.Min(6, (int)Math.Floor(len / 40)));
            for (int k = 0; k <= steps; k++)
            {
                double tt = inset + (1 - 2 * inset) * ((double)k / steps);
                double pxp = a.X + dx * tt, pyp = a.Y + dy * tt;
                if (Math.Abs(dy) >= Math.Abs(dx))
                {
                    Consider(pxp - LabelGap - hw, pyp, "end", i);
                    Consider(pxp + LabelGap + hw, pyp, "start", i);
                }
                else
                {
                    Consider(pxp, pyp - LabelGap - hh, "middle", i);
                    Consider(pxp, pyp + LabelGap + hh, "middle", i);
                }
            }
        }

        if (bestClear < 0)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Point a = points[i], b = points[i + 1];
                if (StepRound.Dist(a, b) < 1) continue;
                var (cx, cy) = Clamp((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                var box = new Box(cx, cy, hw, hh, "middle");
                double clear = Clearance(box, obstacles, lines);
                for (int j = 0; j < points.Count - 1; j++) { if (j == i) continue; clear = Math.Min(clear, SegGap(box, points[j], points[j + 1])); }
                if (clear > bestClear) { best = box; bestClear = clear; }
            }
        }

        if (best is null)
        {
            var (cx, cy) = Clamp(mid.X, mid.Y - LabelGap - hh);
            return new Box(cx, cy, hw, hh, "middle");
        }
        return best;
    }
}
