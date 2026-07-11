using Beck.Layout;
using Beck.Model;
using Beck.Route;
using Beck.Text;
using DiagramModel = Beck.Model.DiagramModel;
using Side = Beck.Authoring.Side;

namespace Beck.Tests.CleanLines;

/// <summary>A single measured defect on one diagram.</summary>
internal sealed record Defect(string Kind, string Detail)
{
    /// <summary>
    /// Hard defects are always wrong and gate the build. <c>through-node</c> joined them once the
    /// router gained a third escape for a back edge whose rank is blocked on both sides: the
    /// direct opposite faces plus a lane detour through the empty inter-rank gaps. Every fuzzed
    /// diagram now routes without cutting a card, so a single one is a regression, not a tolerance.
    /// </summary>
    public bool IsHard => Kind is "off-canvas" or "anchor-off-face" or "anchor-on-corner"
        or "diagonal" or "through-node";
}

/// <summary>
/// The line-quality scorecard for one routed diagram: hard violations (things that are
/// always wrong) plus soft aesthetics (things we want to drive toward zero).
/// </summary>
internal sealed record QualityReport(
    int Edges,
    int StraightEdges,
    int MicroJogs,
    int Bends,
    int Faces,
    int SkewedFaces,
    int OffCenterFaces,
    int MergedRuns,
    int TightEdges,
    IReadOnlyList<Defect> Violations)
{
    public double StraightRate => Edges == 0 ? 1 : (double)StraightEdges / Edges;
    public double BendsPerEdge => Edges == 0 ? 0 : (double)Bends / Edges;
}

/// <summary>
/// Measures how *clean* a routed diagram looks. Two tiers:
///
/// <para><b>Violations</b> — never acceptable, asserted hard by the chaos monkey:
/// off-canvas coordinates, an anchor sitting off (or on the corner of) its node face,
/// a path cutting through a node that isn't one of its endpoints, and a non-orthogonal
/// segment on a step-round edge.</para>
///
/// <para><b>Aesthetics</b> — counted and ratcheted, never asserted per-diagram:
/// micro-jogs (a stub cross-axis segment that reads as a kink rather than a turn),
/// skewed anchor fans (unequal spacing on a shared face), off-center fans, and merged
/// runs (two edges sharing a collinear channel so they read as one wire).</para>
///
/// A jog is only "micro" below <see cref="MicroJogPx"/> — beyond that it is a genuine
/// turn between two ranks and drawing it as a step is correct.
/// </summary>
internal static class LineQuality
{
    /// <summary>A cross-axis run shorter than this reads as a kink, not a deliberate turn.</summary>
    public const double MicroJogPx = 28;

    /// <summary>How far an anchor must stay off its face's corners to still read as "on the face".</summary>
    public const double CornerInset = 2;

    /// <summary>Slack for calling two anchor gaps equal, or a fan centered.</summary>
    public const double FanEps = 1.0;

    /// <summary>Collinear runs closer than this on the perpendicular axis merge into one wire.</summary>
    public const double MergeEps = 1.5;

    /// <summary>Overlap along a shared channel below this is a corner touch, not a merged wire.</summary>
    public const double MergeOverlapPx = 8;

    /// <summary>An edge grazing a node it has no business touching, closer than this, reads as crowded.</summary>
    public const double TightPx = 12;

    public static (DiagramModel Model, LayoutResult Layout, IReadOnlyList<RoutedEdge> Edges) Route(string yaml)
    {
        var model = Validate.LoadDiagram(yaml);
        var sizes = model.Nodes.ToDictionary(n => n.Id, n => CardSizer.Measure(n, InterMetricsMeasurer.Instance));
        var layout = LayeredLayout.Compute(model, sizes);
        return (model, layout, EdgePainter.RouteEdges(model, layout));
    }

    public static QualityReport Analyze(string yaml) => Analyze(Route(yaml));

    public static QualityReport Analyze((DiagramModel Model, LayoutResult Layout, IReadOnlyList<RoutedEdge> Edges) routed)
    {
        var (model, layout, edges) = routed;
        var violations = new List<Defect>();
        int straight = 0, microJogs = 0, bends = 0, mergedRuns = 0, tight = 0;

        var selfLoops = new HashSet<string>();
        foreach (var e in model.Edges)
        {
            if (e.From == e.To)
            {
                selfLoops.Add(e.Id);
            }
        }

        // ---- per-edge shape ----
        var runs = new List<(string Edge, Point A, Point B)>();
        foreach (var re in edges)
        {
            var pts = Dedupe(re.Points);
            foreach (var p in pts)
            {
                if (p.X < -0.01 || p.Y < -0.01)
                {
                    violations.Add(new Defect("off-canvas", $"{re.Edge.Id} passes ({p.X:0.##}, {p.Y:0.##})"));
                }
            }

            if (pts.Count < 2)
            {
                continue;
            }

            var ortho = re.Edge.Curve == EdgeCurve.StepRound;

            for (var i = 0; i < pts.Count - 1; i++)
            {
                Point a = pts[i], b = pts[i + 1];
                if (ortho && Math.Abs(a.X - b.X) > 0.5 && Math.Abs(a.Y - b.Y) > 0.5)
                {
                    violations.Add(new Defect("diagonal", $"{re.Edge.Id} segment ({a.X:0.#},{a.Y:0.#})→({b.X:0.#},{b.Y:0.#})"));
                }

                if (ortho && !selfLoops.Contains(re.Edge.Id))
                {
                    runs.Add((re.Edge.Id, a, b));
                }
            }

            if (selfLoops.Contains(re.Edge.Id))
            {
                continue;
            }

            bends += pts.Count - 2;
            if (pts.Count == 2)
            {
                straight++;
            }

            // Interior segments only: the first and last runs leave/enter a face, so a short one
            // there is an anchor stub, not a jog. A short interior run is the kink we hunt.
            for (var i = 1; i < pts.Count - 2; i++)
            {
                var len = Dist(pts[i], pts[i + 1]);
                if (len > 0.5 && len < MicroJogPx)
                {
                    microJogs++;
                }
            }

            CheckObstacles(layout, re, violations);
            if (Clearance(layout, re) < TightPx)
            {
                tight++;
            }
        }

        // ---- merged runs: two different edges sharing a collinear channel ----
        for (var i = 0; i < runs.Count; i++)
        {
            for (var j = i + 1; j < runs.Count; j++)
            {
                if (runs[i].Edge == runs[j].Edge)
                {
                    continue;
                }

                if (SharesChannel(runs[i], runs[j]))
                {
                    mergedRuns++;
                }
            }
        }

        // ---- anchor fans ----
        var (faces, skewed, offCenter, fanViolations) = AnalyzeFaces(model, layout, edges, selfLoops);
        violations.AddRange(fanViolations);

        var nonLoop = edges.Count(e => !selfLoops.Contains(e.Edge.Id));
        return new QualityReport(nonLoop, straight, microJogs, bends, faces, skewed, offCenter, mergedRuns, tight, violations);
    }

    /// <summary>
    /// The polyline as drawn: coincident points dropped, then collinear interior points
    /// dissolved. A step whose two runs share an axis is one straight line with a redundant
    /// vertex, and counting that vertex as a bend would hide exactly the edges we are trying
    /// to make straight.
    /// </summary>
    private static List<Point> Dedupe(IReadOnlyList<Point> pts)
    {
        var outp = new List<Point>();
        foreach (var p in pts)
        {
            if (outp.Count == 0 || Dist(outp[^1], p) > 0.5)
            {
                outp.Add(p);
            }
        }

        for (var i = outp.Count - 2; i >= 1; i--)
        {
            Point a = outp[i - 1], b = outp[i], c = outp[i + 1];
            var vert = Math.Abs(a.X - b.X) < 0.5 && Math.Abs(b.X - c.X) < 0.5;
            var horz = Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(b.Y - c.Y) < 0.5;
            if (vert || horz)
            {
                outp.RemoveAt(i);
            }
        }
        return outp;
    }

    private static double Dist(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    /// <summary>Two axis-aligned runs on the same line, overlapping enough to read as one wire.</summary>
    private static bool SharesChannel((string Edge, Point A, Point B) r, (string Edge, Point A, Point B) s)
    {
        bool rVert = Math.Abs(r.A.X - r.B.X) < 0.5, sVert = Math.Abs(s.A.X - s.B.X) < 0.5;
        bool rHorz = Math.Abs(r.A.Y - r.B.Y) < 0.5, sHorz = Math.Abs(s.A.Y - s.B.Y) < 0.5;
        if (rVert && sVert && !(rHorz && sHorz))
        {
            if (Math.Abs(r.A.X - s.A.X) > MergeEps)
            {
                return false;
            }

            return Overlap(r.A.Y, r.B.Y, s.A.Y, s.B.Y) > MergeOverlapPx;
        }
        if (rHorz && sHorz && !(rVert && sVert))
        {
            if (Math.Abs(r.A.Y - s.A.Y) > MergeEps)
            {
                return false;
            }

            return Overlap(r.A.X, r.B.X, s.A.X, s.B.X) > MergeOverlapPx;
        }
        return false;
    }

    private static double Overlap(double a1, double a2, double b1, double b2) =>
        Math.Min(Math.Max(a1, a2), Math.Max(b1, b2)) - Math.Max(Math.Min(a1, a2), Math.Min(b1, b2));

    /// <summary>
    /// The closest this edge comes to a node it neither starts nor ends at. A route that clears
    /// every obstacle can still look cramped — the router only ever asked "does this hit?", never
    /// "how much room did it leave?". Endpoints are excluded: an edge is *supposed* to touch those.
    /// </summary>
    private static double Clearance(LayoutResult layout, RoutedEdge re)
    {
        var pts = Dedupe(re.Points);
        var min = double.PositiveInfinity;
        foreach (var (id, rect) in layout.Nodes)
        {
            if (id == re.Edge.From || id == re.Edge.To)
            {
                continue;
            }

            for (var i = 0; i < pts.Count - 1; i++)
            {
                min = Math.Min(min, SegRectDist(pts[i], pts[i + 1], rect));
            }
        }
        return min;
    }

    /// <summary>Distance from an axis-aligned segment to a rect; 0 when they touch or overlap.</summary>
    private static double SegRectDist(Point a, Point b, Rect r)
    {
        double x1 = r.X, y1 = r.Y, x2 = r.X + r.W, y2 = r.Y + r.H;
        // Segment's own bounding box (it is axis-aligned, so this is exact).
        double sx1 = Math.Min(a.X, b.X), sx2 = Math.Max(a.X, b.X);
        double sy1 = Math.Min(a.Y, b.Y), sy2 = Math.Max(a.Y, b.Y);
        var dx = Math.Max(0, Math.Max(x1 - sx2, sx1 - x2));
        var dy = Math.Max(0, Math.Max(y1 - sy2, sy1 - y2));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static void CheckObstacles(LayoutResult layout, RoutedEdge re, List<Defect> violations)
    {
        var pts = Dedupe(re.Points);
        foreach (var (id, rect) in layout.Nodes)
        {
            if (id == re.Edge.From || id == re.Edge.To)
            {
                continue;
            }

            for (var i = 0; i < pts.Count - 1; i++)
            {
                if (SegHitsRect(pts[i], pts[i + 1], rect))
                {
                    violations.Add(new Defect("through-node", $"{re.Edge.Id} crosses node '{id}'"));
                    return;
                }
            }
        }
    }

    // Mirrors OrthogonalRouter.SegHitsRect, with a slightly larger inset so a path grazing a
    // node's stroke isn't reported. Only a genuine cut through the card counts.
    private static bool SegHitsRect(Point a, Point b, Rect rect, double inset = 4)
    {
        double x1 = rect.X + inset, y1 = rect.Y + inset;
        double x2 = rect.X + rect.W - inset, y2 = rect.Y + rect.H - inset;
        if (x2 <= x1 || y2 <= y1)
        {
            return false;
        }

        if (Math.Abs(a.Y - b.Y) < 0.5)
        {
            var y = a.Y;
            if (y <= y1 || y >= y2)
            {
                return false;
            }

            return Math.Max(a.X, b.X) > x1 && Math.Min(a.X, b.X) < x2;
        }
        if (Math.Abs(a.X - b.X) < 0.5)
        {
            var x = a.X;
            if (x <= x1 || x >= x2)
            {
                return false;
            }

            return Math.Max(a.Y, b.Y) > y1 && Math.Min(a.Y, b.Y) < y2;
        }
        return Math.Max(a.X, b.X) > x1 && Math.Min(a.X, b.X) < x2 && Math.Max(a.Y, b.Y) > y1 && Math.Min(a.Y, b.Y) < y2;
    }

    /// <summary>
    /// Recover each edge's anchors from its routed endpoints, bucket them by the node face
    /// they land on, and measure the fan: anchors must sit on the face (never past a corner),
    /// consecutive gaps must be equal, and the spread must center on the face.
    /// </summary>
    private static (int Faces, int Skewed, int OffCenter, List<Defect> Violations) AnalyzeFaces(
        DiagramModel model, LayoutResult layout, IReadOnlyList<RoutedEdge> edges, HashSet<string> selfLoops)
    {
        var violations = new List<Defect>();
        var buckets = new Dictionary<(string Node, Side Side), List<double>>();

        // A parallelogram's left/right faces are slanted: the router nudges those anchors inward
        // by skew/2 (see Svg/Artwork.cs ParallelogramSkew + Route/EdgePainter.cs paraSkew) so they
        // land on the slant instead of the bbox edge. Top/bottom faces are unslanted — no nudge.
        var paraNudge = model.Nodes
            .Where(n => n.Shape is NodeShape.Parallelogram && layout.Nodes.ContainsKey(n.Id))
            .ToDictionary(n => n.Id, n => Math.Min(12, layout.Nodes[n.Id].H * 0.4) / 2);

        void Record(string nodeId, Point p, string edgeId)
        {
            if (!layout.Nodes.TryGetValue(nodeId, out var r))
            {
                return;
            }

            var nudge = paraNudge.GetValueOrDefault(nodeId);
            var side = FaceOf(r, p, nudge);
            if (side is null)
            {
                violations.Add(new Defect("anchor-off-face",
                    $"{edgeId} anchors at ({p.X:0.##}, {p.Y:0.##}) — not on any face of '{nodeId}' {Fmt(r)}"));
                return;
            }
            var alongY = side is Side.Left or Side.Right;
            var pos = alongY ? p.Y : p.X;
            double lo = alongY ? r.Y : r.X, hi = alongY ? r.Y + r.H : r.X + r.W;
            if (pos < lo + CornerInset - 0.01 || pos > hi - CornerInset + 0.01)
            {
                violations.Add(new Defect("anchor-on-corner",
                    $"{edgeId} anchors at {pos:0.##} on the {side} face of '{nodeId}' (span {lo:0.##}..{hi:0.##})"));
            }

            var key = (nodeId, side.Value);
            if (!buckets.TryGetValue(key, out var list))
            {
                buckets[key] = list = new();
            }

            list.Add(pos);
        }

        foreach (var re in edges)
        {
            if (selfLoops.Contains(re.Edge.Id) || re.Points.Count < 2)
            {
                continue;
            }

            Record(re.Edge.From, re.Points[0], re.Edge.Id);
            Record(re.Edge.To, re.Points[^1], re.Edge.Id);
        }

        int skewed = 0, offCenter = 0, faces = 0;
        foreach (var (key, raw) in buckets)
        {
            if (raw.Count < 2)
            {
                continue;
            }

            faces++;
            var pos = raw.OrderBy(v => v).ToList();
            var r = layout.Nodes[key.Node];
            var alongY = key.Side is Side.Left or Side.Right;
            var center = alongY ? r.Y + r.H / 2 : r.X + r.W / 2;

            var gaps = new List<double>();
            for (var i = 1; i < pos.Count; i++)
            {
                gaps.Add(pos[i] - pos[i - 1]);
            }

            if (gaps.Max() - gaps.Min() > FanEps)
            {
                skewed++;
            }

            if (Math.Abs((pos[0] + pos[^1]) / 2 - center) > FanEps)
            {
                offCenter++;
            }
        }
        return (faces, skewed, offCenter, violations);
    }

    /// <param name="nudge">
    /// A parallelogram's left/right anchors sit <c>nudge</c> px inward of the bbox edge, on the
    /// shape's slant, rather than exactly on it (0 for every other shape).
    /// </param>
    private static Side? FaceOf(Rect r, Point p, double nudge = 0)
    {
        const double Eps = 0.6;
        var inX = p.X >= r.X - Eps && p.X <= r.X + r.W + Eps;
        var inY = p.Y >= r.Y - Eps && p.Y <= r.Y + r.H + Eps;
        if (inX && Math.Abs(p.Y - r.Y) < Eps)
        {
            return Side.Top;
        }

        if (inX && Math.Abs(p.Y - (r.Y + r.H)) < Eps)
        {
            return Side.Bottom;
        }

        if (inY && Math.Abs(p.X - (r.X + nudge)) < Eps)
        {
            return Side.Left;
        }

        if (inY && Math.Abs(p.X - (r.X + r.W - nudge)) < Eps)
        {
            return Side.Right;
        }

        return null;
    }

    private static string Fmt(Rect r) => $"[{r.X:0.##},{r.Y:0.##} {r.W:0.##}×{r.H:0.##}]";
}