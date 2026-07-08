namespace Beck.Rendering.Route;

/// <summary>A routed edge: the model edge plus its path data + turn points.</summary>
internal sealed record RoutedEdge(EdgeModel Edge, string D, IReadOnlyList<Point> Points);

/// <summary>
/// Routes every edge in a model over a layout — the path-computation half of
/// <c>src/route/svg.ts:routeEdges</c> (prep → anchor spread → routeEdge). Marker
/// and label emission land in M5; this produces the animatable path <c>d</c>.
/// </summary>
internal static class EdgePainter
{
    private sealed record EdgePrep(Rect From, Rect To, List<Rect> Obstacles, Side FromSide, Side ToSide);

    public static List<RoutedEdge> RouteEdges(DiagramModel model, LayoutResult layout)
    {
        double radius = model.Meta.Spacing.CornerRadius;
        Direction dir = model.Meta.Direction;
        bool primaryHorizontal = dir is Direction.LR or Direction.RL;
        var groupById = model.Groups.ToDictionary(g => g.Id);

        Rect? RectOf(string id) =>
            layout.Nodes.TryGetValue(id, out var n) ? n
            : layout.Groups.TryGetValue(id, out var g) ? g : null;

        // All leaf-node ids under a group (recursing nested sub-groups).
        List<string> MemberNodeIds(string id)
        {
            var outp = new List<string>();
            if (!groupById.ContainsKey(id)) return outp;
            void Walk(string gid)
            {
                foreach (var m in groupById.TryGetValue(gid, out var grp) ? grp.Members : new List<string>())
                {
                    if (layout.Nodes.ContainsKey(m)) outp.Add(m);
                    else if (groupById.ContainsKey(m)) Walk(m);
                }
            }
            Walk(id);
            return outp;
        }

        var prep = new EdgePrep?[model.Edges.Count];
        for (int i = 0; i < model.Edges.Count; i++)
        {
            EdgeModel edge = model.Edges[i];
            Rect? from = RectOf(edge.From), to = RectOf(edge.To);
            if (from is null || to is null) { prep[i] = null; continue; }

            var excludeIds = new HashSet<string>();
            if (layout.Nodes.ContainsKey(edge.From)) excludeIds.Add(edge.From);
            if (layout.Nodes.ContainsKey(edge.To)) excludeIds.Add(edge.To);
            foreach (var m in MemberNodeIds(edge.From)) excludeIds.Add(m);
            foreach (var m in MemberNodeIds(edge.To)) excludeIds.Add(m);
            var obstacles = layout.Nodes.Where(kv => !excludeIds.Contains(kv.Key)).Select(kv => kv.Value).ToList();

            var (fs, ts) = OrthogonalRouter.SidesFor(from.Value, to.Value, dir, edge.Curve, obstacles, edge.FromSide, edge.ToSide);
            prep[i] = new EdgePrep(from.Value, to.Value, obstacles, fs, ts);
        }

        // Terminal dots (state [*] start/end, 16×16) are points, not faces — spreading
        // several edges across one just bends each into a tiny jog. Anchor them all at
        // centre so a lone incoming edge can run straight into the dot.
        var pointNodes = model.Nodes
            .Where(n => n.Shape is NodeShape.Start or NodeShape.End)
            .Select(n => n.Id).ToHashSet();
        var shifts = AnchorShifts(model.Edges, prep, pointNodes);

        var outEdges = new List<RoutedEdge>();
        for (int i = 0; i < model.Edges.Count; i++)
        {
            EdgePrep? p = prep[i];
            if (p is null) continue;
            EdgeModel edge = model.Edges[i];
            RoutedPath routed = OrthogonalRouter.RouteEdge(new RouteRequest(
                p.From, p.To, p.FromSide, p.ToSide, edge.Curve, p.Obstacles, radius, primaryHorizontal,
                new Size(layout.Width, layout.Height), shifts[i].From, shifts[i].To));
            outEdges.Add(new RoutedEdge(edge, routed.D, routed.Points));
        }
        return outEdges;
    }

    /// <summary>
    /// Spread anchors of edges sharing a node face along it, ordered by their far
    /// endpoints — a port of <c>svg.ts:anchorShifts</c>. Keeps fan-in/out lines
    /// from stacking and A↔B pairs parallel.
    /// </summary>
    private static (double From, double To)[] AnchorShifts(
        IReadOnlyList<EdgeModel> edges, EdgePrep?[] prep, HashSet<string> pointNodes)
    {
        var shifts = new (double From, double To)[edges.Count];
        var groups = new Dictionary<(string Node, Side Side), List<(int Idx, bool IsFrom)>>();
        void Add(string nodeId, Side side, int idx, bool isFrom)
        {
            var key = (nodeId, side);
            if (!groups.TryGetValue(key, out var g)) groups[key] = g = new();
            g.Add((idx, isFrom));
        }
        for (int i = 0; i < edges.Count; i++)
        {
            EdgeModel e = edges[i];
            if (e.From == e.To) continue;
            EdgePrep? p = prep[i];
            if (p is null) continue;
            Add(e.From, p.FromSide, i, true);
            Add(e.To, p.ToSide, i, false);
        }

        Rect FaceRect((int Idx, bool IsFrom) r) => r.IsFrom ? prep[r.Idx]!.From : prep[r.Idx]!.To;
        Rect FarRect((int Idx, bool IsFrom) r) => r.IsFrom ? prep[r.Idx]!.To : prep[r.Idx]!.From;

        foreach (var (key, refs) in groups)
        {
            if (refs.Count < 2 || pointNodes.Contains(key.Node)) continue;
            Side side = key.Side;
            Rect rect = FaceRect(refs[0]);
            bool alongY = side is Side.Left or Side.Right;
            double faceLen = alongY ? rect.H : rect.W;
            double faceCenter = alongY ? rect.Y + rect.H / 2 : rect.X + rect.W / 2;
            double step = Math.Min(20, faceLen * 0.7 / (refs.Count - 1));
            double FarCenter((int Idx, bool IsFrom) r)
            {
                Rect other = FarRect(r);
                return alongY ? other.Y + other.H / 2 : other.X + other.W / 2;
            }

            // Order by far endpoint; ties (same far node) by edge id — direction-stable.
            var sorted = refs.OrderBy(x => x, Comparer<(int Idx, bool IsFrom)>.Create((r1, r2) =>
            {
                double d = FarCenter(r1) - FarCenter(r2);
                if (Math.Abs(d) > 0.5) return d < 0 ? -1 : 1;
                return string.CompareOrdinal(edges[r1.Idx].Id, edges[r2.Idx].Id) < 0 ? -1 : 1;
            })).ToList();

            const double alignEps = 4;
            double half = faceLen * 0.7 / 2;
            double @base = (sorted.Count - 1) / 2.0;
            int alignIdx = -1, alignCount = 0;
            double bestDist = alignEps;
            for (int i = 0; i < sorted.Count; i++)
            {
                double d = Math.Abs(FarCenter(sorted[i]) - faceCenter);
                if (d > alignEps) continue;
                alignCount++;
                if (d <= bestDist) { bestDist = d; alignIdx = i; }
            }
            if (alignCount == 1 && alignIdx >= 0
                && -alignIdx * step >= -half - 0.01
                && (sorted.Count - 1 - alignIdx) * step <= half + 0.01)
                @base = alignIdx;

            for (int i = 0; i < sorted.Count; i++)
            {
                double off = (i - @base) * step;
                if (sorted[i].IsFrom) shifts[sorted[i].Idx].From = off;
                else shifts[sorted[i].Idx].To = off;
            }
        }
        return shifts;
    }
}
