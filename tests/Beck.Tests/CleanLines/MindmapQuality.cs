using Beck.Layout;
using Beck.Model;
using Beck.Route;
using Beck.Text;
using DiagramModel = Beck.Model.DiagramModel;

namespace Beck.Tests.CleanLines;

/// <summary>
/// Soft aesthetics for one routed mindmap, ratcheted against <c>Goldens/cleanlines.json</c>'s
/// <c>Mindmap</c> key. Butterfly-specific, so nothing overlaps the architecture/flowchart baselines:
/// how much canvas each node costs, how long the mean parent→child thread runs, and how lopsided the
/// two halves are (heavier half total height ÷ lighter half — 1.0 is a perfect balance).
/// </summary>
internal sealed record MindmapMetrics(double AreaPerNode, double MeanEdgeLength, double HalfBalance);

/// <summary>The scorecard for one routed mindmap: hard violations plus the soft <see cref="MindmapMetrics"/>.</summary>
internal sealed record MindmapReport(int Nodes, int Edges, IReadOnlyList<Defect> Violations, MindmapMetrics Metrics);

/// <summary>
/// The mindmap chaos monkey's analyzer. Mindmap edges are S-curves — a single cubic Bézier with only
/// two endpoint points — so the orthogonal <see cref="LineQuality"/> checks (diagonal-on-step-round,
/// through-node, anchor fans) don't apply. This measures the butterfly's own invariants instead:
///
/// <list type="bullet">
/// <item><b>off-canvas</b> — a node rect corner or a routed path point below −0.01.</item>
/// <item><b>node-overlap</b> — any two node rects genuinely intersecting, across both halves.</item>
/// <item><b>tree-shape</b> — edges = nodes − 1, and every edge runs from the parent's outward face
/// into the child's inward face, the two faces pointing at each other (verified against the actual
/// left/right x-order of each pair, never hardcoded).</item>
/// <item><b>root-centering</b> — with both halves populated, the root's centre x sits strictly
/// between the left half's rightmost edge and the right half's leftmost edge.</item>
/// </list>
/// </summary>
internal static class MindmapQuality
{
    /// <summary>Genuine rect intersection must exceed this on both axes to count as an overlap.</summary>
    private const double OverlapEps = 0.01;

    /// <summary>How close a path endpoint must sit to a face line to read as "on that face".</summary>
    private const double FaceEps = 1.0;

    public static (DiagramModel Model, LayoutResult Layout, IReadOnlyList<RoutedEdge> Edges) Route(string yaml)
    {
        var model = Validate.LoadDiagram(yaml);
        var sizes = model.Nodes.ToDictionary(n => n.Id, n => CardSizer.Measure(n, InterMetricsMeasurer.Instance, mindMap: true));
        var layout = MindMapLayout.Compute(model, sizes);
        return (model, layout, EdgePainter.RouteEdges(model, layout));
    }

    public static MindmapReport Analyze(string yaml) => Analyze(Route(yaml));

    public static MindmapReport Analyze((DiagramModel Model, LayoutResult Layout, IReadOnlyList<RoutedEdge> Edges) routed)
    {
        var (model, layout, edges) = routed;
        var violations = new List<Defect>();

        // ---- off-canvas: node rects and routed path points ----
        foreach (var (id, r) in layout.Nodes)
        {
            if (r.X < -0.01 || r.Y < -0.01)
            {
                violations.Add(new Defect("off-canvas", $"node '{id}' at ({r.X:0.##}, {r.Y:0.##})"));
            }
        }

        foreach (var re in edges)
        {
            foreach (var p in re.Points)
            {
                if (p.X < -0.01 || p.Y < -0.01)
                {
                    violations.Add(new Defect("off-canvas", $"{re.Edge.Id} passes ({p.X:0.##}, {p.Y:0.##})"));
                }
            }
        }

        // ---- node-overlap: any pairwise rect intersection, across both halves ----
        var rects = layout.Nodes.OrderBy(kv => kv.Key).ToList();
        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                if (Intersects(rects[i].Value, rects[j].Value))
                {
                    violations.Add(new Defect("node-overlap",
                        $"'{rects[i].Key}' {Fmt(rects[i].Value)} overlaps '{rects[j].Key}' {Fmt(rects[j].Value)}"));
                }
            }
        }

        // ---- tree-shape: edge count and per-edge face geometry ----
        if (edges.Count != layout.Nodes.Count - 1)
        {
            violations.Add(new Defect("tree-shape",
                $"{edges.Count} edges over {layout.Nodes.Count} nodes (expected {layout.Nodes.Count - 1})"));
        }

        foreach (var re in edges)
        {
            if (!layout.Nodes.TryGetValue(re.Edge.From, out var parent)
                || !layout.Nodes.TryGetValue(re.Edge.To, out var child)
                || re.Points.Count < 2)
            {
                continue;
            }

            var from = re.Points[0];
            var to = re.Points[^1];
            var childOnRight = Center(child).X > Center(parent).X;

            // Right-half child: parent's RIGHT face → child's LEFT face. Left-half: mirrored. The two
            // faces must point at each other, and each endpoint must land on its own face line within
            // the node's vertical span.
            var parentFaceX = childOnRight ? parent.X + parent.W : parent.X;
            var childFaceX = childOnRight ? child.X : child.X + child.W;

            if (!OnVerticalFace(from, parentFaceX, parent))
            {
                violations.Add(new Defect("tree-shape",
                    $"{re.Edge.Id} leaves ({from.X:0.##}, {from.Y:0.##}) — not on the {(childOnRight ? "right" : "left")} face of '{re.Edge.From}' {Fmt(parent)}"));
            }
            if (!OnVerticalFace(to, childFaceX, child))
            {
                violations.Add(new Defect("tree-shape",
                    $"{re.Edge.Id} enters ({to.X:0.##}, {to.Y:0.##}) — not on the {(childOnRight ? "left" : "right")} face of '{re.Edge.To}' {Fmt(child)}"));
            }
        }

        // ---- root-centering: root strictly between the two halves ----
        var rootId = model.Nodes.FirstOrDefault(n => n.Rank == 0)?.Id ?? model.Nodes[0].Id;
        if (layout.Nodes.TryGetValue(rootId, out var root))
        {
            var rootCx = Center(root).X;
            var left = layout.Nodes.Where(kv => kv.Key != rootId && Center(kv.Value).X < rootCx).Select(kv => kv.Value).ToList();
            var right = layout.Nodes.Where(kv => kv.Key != rootId && Center(kv.Value).X > rootCx).Select(kv => kv.Value).ToList();
            if (left.Count > 0 && right.Count > 0)
            {
                var leftMaxRight = left.Max(r => r.X + r.W);
                var rightMinLeft = right.Min(r => r.X);
                if (!(rootCx > leftMaxRight && rootCx < rightMinLeft))
                {
                    violations.Add(new Defect("root-centering",
                        $"root centre {rootCx:0.##} not strictly between left edge {leftMaxRight:0.##} and right edge {rightMinLeft:0.##}"));
                }
            }
        }

        return new MindmapReport(layout.Nodes.Count, edges.Count, violations, Metrics(model, layout, edges, rootId));
    }

    private static MindmapMetrics Metrics(
        DiagramModel model, LayoutResult layout, IReadOnlyList<RoutedEdge> edges, string rootId)
    {
        var n = Math.Max(1, layout.Nodes.Count);
        var areaPerNode = layout.Width * layout.Height / n;

        double edgeLen = 0;
        var edgeN = 0;
        foreach (var re in edges)
        {
            if (re.Points.Count < 2)
            {
                continue;
            }

            var a = re.Points[0];
            var b = re.Points[^1];
            edgeLen += Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
            edgeN++;
        }
        var meanEdge = edgeN == 0 ? 0 : edgeLen / edgeN;

        double leftH = 0, rightH = 0;
        if (layout.Nodes.TryGetValue(rootId, out var root))
        {
            var rootCx = Center(root).X;
            foreach (var (id, r) in layout.Nodes)
            {
                if (id == rootId)
                {
                    continue;
                }

                if (Center(r).X < rootCx)
                {
                    leftH += r.H;
                }
                else
                {
                    rightH += r.H;
                }
            }
        }
        var heavier = Math.Max(leftH, rightH);
        var lighter = Math.Min(leftH, rightH);
        // A single-branch (one-sided) butterfly has nothing to balance — that lopsidedness is inherent
        // to the layout, not a defect — so it reads as neutral (1.0). Only when both halves carry
        // weight does the ratio measure how evenly the balancer split them.
        var balance = leftH <= 0.01 || rightH <= 0.01 ? 1.0 : heavier / lighter;

        return new MindmapMetrics(areaPerNode, meanEdge, balance);
    }

    private static Point Center(Rect r) => new(r.X + r.W / 2, r.Y + r.H / 2);

    private static bool Intersects(Rect a, Rect b)
    {
        var ox = Math.Min(a.X + a.W, b.X + b.W) - Math.Max(a.X, b.X);
        var oy = Math.Min(a.Y + a.H, b.Y + b.H) - Math.Max(a.Y, b.Y);
        return ox > OverlapEps && oy > OverlapEps;
    }

    private static bool OnVerticalFace(Point p, double faceX, Rect r) =>
        Math.Abs(p.X - faceX) <= FaceEps && p.Y >= r.Y - FaceEps && p.Y <= r.Y + r.H + FaceEps;

    private static string Fmt(Rect r) => $"[{r.X:0.##},{r.Y:0.##} {r.W:0.##}×{r.H:0.##}]";
}
