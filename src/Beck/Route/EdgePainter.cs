using Beck.Layout;
using Beck.Model;
using DiagramModel = Beck.Model.DiagramModel;
using Direction = Beck.Model.Direction;
using EdgeModel = Beck.Model.EdgeModel;

namespace Beck.Route;

/// <summary>A routed edge: the model edge plus its path data + turn points.</summary>
internal sealed record RoutedEdge(EdgeModel Edge, string D, IReadOnlyList<Point> Points);

/// <summary>
/// How one end of an edge meets its node face: how far the anchor slid along the face,
/// whether the face is shared with other edges, and which turn lane the edge takes.
/// A lone anchor is the default — centred, unfanned, single-lane.
/// </summary>
internal readonly record struct AnchorPlan(double Shift, bool Fanned, Lane Lane);

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
        var radius = model.Meta.Spacing.CornerRadius;
        var dir = model.Meta.Direction;
        var primaryHorizontal = dir is Direction.Lr or Direction.Rl;
        var groupById = model.Groups.ToDictionary(g => g.Id);

        Rect? RectOf(string id) =>
            layout.Nodes.TryGetValue(id, out var n) ? n
            : layout.Groups.TryGetValue(id, out var g) ? g : null;

        // All leaf-node ids under a group (recursing nested sub-groups).
        List<string> MemberNodeIds(string id)
        {
            var outp = new List<string>();
            if (!groupById.ContainsKey(id))
            {
                return outp;
            }

            void Walk(string gid)
            {
                foreach (var m in groupById.TryGetValue(gid, out var grp) ? grp.Members : new List<string>())
                {
                    if (layout.Nodes.ContainsKey(m))
                    {
                        outp.Add(m);
                    }
                    else if (groupById.ContainsKey(m))
                    {
                        Walk(m);
                    }
                }
            }
            Walk(id);
            return outp;
        }

        var prep = new EdgePrep?[model.Edges.Count];
        for (var i = 0; i < model.Edges.Count; i++)
        {
            var edge = model.Edges[i];
            Rect? from = RectOf(edge.From), to = RectOf(edge.To);
            if (from is null || to is null) { prep[i] = null; continue; }

            var excludeIds = new HashSet<string>();
            if (layout.Nodes.ContainsKey(edge.From))
            {
                excludeIds.Add(edge.From);
            }

            if (layout.Nodes.ContainsKey(edge.To))
            {
                excludeIds.Add(edge.To);
            }

            foreach (var m in MemberNodeIds(edge.From))
            {
                excludeIds.Add(m);
            }

            foreach (var m in MemberNodeIds(edge.To))
            {
                excludeIds.Add(m);
            }

            var obstacles = layout.Nodes.Where(kv => !excludeIds.Contains(kv.Key)).Select(kv => kv.Value).ToList();

            var (fs, ts) = OrthogonalRouter.SidesFor(
                from.Value, to.Value, dir, edge.Curve, obstacles, edge.FromSide, edge.ToSide,
                new Size(layout.Width, layout.Height));
            prep[i] = new EdgePrep(from.Value, to.Value, obstacles, fs, ts);
        }

        // A decision diamond's edges want DISTINCT vertices (the classic flowchart idiom): a bottom
        // vertex shared by the yes- and no-branch reads as one line forking. Since diamonds are
        // point-anchored (no spread), two edges resolving to the same face land on the identical
        // point and share a trunk. Redistribute the laterally-displaced ones onto the Left/Right
        // (or Top/Bottom) side vertices matching their far endpoint's direction. Flowchart-only so
        // architecture/state/class diamond routing (and its goldens) stays byte-identical.
        // Snapshot the pre-reassignment sides so a reassigned diamond edge that ends up cutting a node
        // (the side-vertex corner route skips the obstacle avoidance the primary-face route has) can be
        // reverted to its clean original route below. null when no reassignment runs.
        var origPrep = model.Meta.Type == DiagramType.Flowchart ? (EdgePrep?[])prep.Clone() : null;
        if (origPrep is not null)
        {
            ReassignDiamondSides(model, prep);
        }

        // Terminal dots (state [*] start/end, 16×16) are points, not faces — spreading
        // several edges across one just bends each into a tiny jog. Anchor them all at
        // centre so a lone incoming edge can run straight into the dot.
        //
        // Diamonds are point-anchored too: each bbox face-midpoint coincides EXACTLY with a diamond
        // vertex, so an anchor must stay pinned to it — never spread (suppressed here), and never slid
        // off by the router's straighten cheat (marked fanned below). Only diamonds get the fanned
        // override so state start/end routing stays byte-identical.
        var diamondNodes = model.Nodes
            .Where(n => n.Shape is NodeShape.Diamond)
            .Select(n => n.Id).ToHashSet();
        var pointNodes = model.Nodes
            .Where(n => n.Shape is NodeShape.Start or NodeShape.End)
            .Select(n => n.Id).ToHashSet();
        pointNodes.UnionWith(diamondNodes);
        // Parallelogram left/right anchors sit on a slanted face: the router nudges them inward by
        // skew/2 in x. Top/bottom faces are unslanted (no nudge). Keyed by node id → skew.
        var paraSkew = model.Nodes
            .Where(n => n.Shape is NodeShape.Parallelogram && layout.Nodes.ContainsKey(n.Id))
            .ToDictionary(n => n.Id, n => Svg.Artwork.ParallelogramSkew(layout.Nodes[n.Id].H) / 2);
        // Mindmap: children fan from their parent's single face midpoint (suppress the anchor spread so
        // siblings share one start point), and each parent→child edge uses a fixed cubic offset — ±40 out
        // of the root, ±35 out of a rank-1+ node (keyed by the parent = edge.From).
        Dictionary<string, double>? mmOffset = null;
        if (model.Meta.Type == DiagramType.MindMap)
        {
            pointNodes.UnionWith(model.Nodes.Select(n => n.Id));
            mmOffset = model.Nodes.ToDictionary(n => n.Id, n => (n.Rank ?? 0) == 0 ? 40.0 : 35.0);
        }

        var shifts = AnchorShifts(model.Edges, prep, pointNodes);

        var outEdges = new List<RoutedEdge>();
        for (var i = 0; i < model.Edges.Count; i++)
        {
            var p = prep[i];
            if (p is null)
            {
                continue;
            }

            var edge = model.Edges[i];
            var (from, to) = shifts[i];
            // A diamond end is pinned to its vertex: mark it fanned (immovable) so straighten leaves it.
            var fromFanned = from.Fanned || diamondNodes.Contains(edge.From);
            var toFanned = to.Fanned || diamondNodes.Contains(edge.To);
            // A parallelogram end's Left/Right anchor slides inward by skew/2 onto the slant (0 otherwise;
            // NudgePerp no-ops Top/Bottom faces).
            var fromNudge = paraSkew.TryGetValue(edge.From, out var fn) ? fn : 0;
            var toNudge = paraSkew.TryGetValue(edge.To, out var tn) ? tn : 0;
            var mmOff = mmOffset != null && mmOffset.TryGetValue(edge.From, out var mo) ? mo : 0;
            RoutedPath Route(Side fromSide, Side toSide) => OrthogonalRouter.RouteEdge(new RouteRequest(
                p.From, p.To, fromSide, toSide, edge.Curve, p.Obstacles, radius, primaryHorizontal,
                new Size(layout.Width, layout.Height), from.Shift, to.Shift,
                fromFanned, toFanned, from.Lane, to.Lane, fromNudge, toNudge, mmOff));

            var routed = Route(p.FromSide, p.ToSide);
            // A diamond edge reassigned to a side vertex (Bug-2 fix) trades the primary-face route's
            // obstacle avoidance for a naive corner; if that corner cuts a node, fall back to the
            // original vertex — correctness (no through-node) over the aesthetic spread.
            if (origPrep?[i] is { } op && (op.FromSide != p.FromSide || op.ToSide != p.ToSide)
                && HitsAnyObstacle(routed.Points, p.Obstacles))
            {
                routed = Route(op.FromSide, op.ToSide);
            }
            outEdges.Add(new RoutedEdge(edge, routed.D, routed.Points));
        }
        return outEdges;
    }

    private static List<(int Idx, bool IsFrom)> Bucket(
        Dictionary<string, List<(int Idx, bool IsFrom)>> map, string key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = new();
        }

        return list;
    }

    /// <summary>Does any segment of this polyline cut through an obstacle rect? Matches the
    /// through-node test the clean-line monitor gates on (a slightly larger inset than the router's
    /// own hit test, so a route that only grazes a stroke is not counted).</summary>
    private static bool HitsAnyObstacle(IReadOnlyList<Point> pts, IReadOnlyList<Rect> obstacles)
    {
        const double Inset = 4;
        for (var i = 0; i < pts.Count - 1; i++)
        {
            Point a = pts[i], b = pts[i + 1];
            foreach (var r in obstacles)
            {
                double x1 = r.X + Inset, y1 = r.Y + Inset, x2 = r.X + r.W - Inset, y2 = r.Y + r.H - Inset;
                if (x2 <= x1 || y2 <= y1)
                {
                    continue;
                }

                bool hit;
                if (Math.Abs(a.Y - b.Y) < 0.5)
                {
                    hit = a.Y > y1 && a.Y < y2 && Math.Max(a.X, b.X) > x1 && Math.Min(a.X, b.X) < x2;
                }
                else if (Math.Abs(a.X - b.X) < 0.5)
                {
                    hit = a.X > x1 && a.X < x2 && Math.Max(a.Y, b.Y) > y1 && Math.Min(a.Y, b.Y) < y2;
                }
                else
                {
                    hit = Math.Max(a.X, b.X) > x1 && Math.Min(a.X, b.X) < x2
                        && Math.Max(a.Y, b.Y) > y1 && Math.Min(a.Y, b.Y) < y2;
                }

                if (hit)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Give a flowchart decision's edges DISTINCT diamond vertices — the classic flowchart idiom.
    /// Diamonds are point-anchored (every bbox face midpoint is a vertex, anchors never spread), so
    /// two edges resolving to the same face land on the identical point and share a trunk out of the
    /// diamond, reading as one line that forks. For each diamond, endpoints on a crowded face keep
    /// the axis-aligned one there and move each laterally-displaced one to the side vertex — Left/Right
    /// for a Top/Bottom face, Top/Bottom for a Left/Right face — matching its far endpoint's direction.
    /// Contended vertices fall back to the least-loaded one (preferring the original face), so where
    /// the four vertices allow it every edge gets its own; overflow piles on the least-loaded vertex.
    /// Rank-aware: a moved cross-rank edge still travels the primary axis after the short lateral hop
    /// off the vertex. Deterministic — ties break on edge id. Flowchart-only, so architecture/state/
    /// class diamond routing (and its frozen goldens) stays byte-identical.
    /// </summary>
    private static void ReassignDiamondSides(DiagramModel model, EdgePrep?[] prep)
    {
        const double LateralEps = 8; // a far endpoint within this of the diamond's axis is "aligned"

        var diamonds = model.Nodes.Where(n => n.Shape == NodeShape.Diamond).Select(n => n.Id).ToHashSet();
        if (diamonds.Count == 0)
        {
            return;
        }

        // Endpoints touching each diamond: (edge index, whether the diamond is this edge's `from`).
        var endpoints = new Dictionary<string, List<(int Idx, bool IsFrom)>>();
        for (var i = 0; i < model.Edges.Count; i++)
        {
            if (prep[i] is null)
            {
                continue;
            }

            var e = model.Edges[i];
            if (e.From == e.To)
            {
                continue;
            }

            if (diamonds.Contains(e.From)) { Bucket(endpoints, e.From).Add((i, true)); }
            if (diamonds.Contains(e.To)) { Bucket(endpoints, e.To).Add((i, false)); }
        }

        foreach (var (_, eps) in endpoints)
        {
            var rect = eps[0].IsFrom ? prep[eps[0].Idx]!.From : prep[eps[0].Idx]!.To;
            double cx = rect.X + rect.W / 2, cy = rect.Y + rect.H / 2;

            Side SideOf((int Idx, bool IsFrom) ep) => ep.IsFrom ? prep[ep.Idx]!.FromSide : prep[ep.Idx]!.ToSide;
            Rect FarOf((int Idx, bool IsFrom) ep) => ep.IsFrom ? prep[ep.Idx]!.To : prep[ep.Idx]!.From;
            // Signed lateral offset of the far endpoint from the diamond centre, on the axis parallel
            // to `side` (X for a Top/Bottom face, Y for a Left/Right one).
            double Lateral((int Idx, bool IsFrom) ep, Side side)
            {
                var far = FarOf(ep);
                return side is Side.Top or Side.Bottom ? far.X + far.W / 2 - cx : far.Y + far.H / 2 - cy;
            }

            var load = new Dictionary<Side, int>
            {
                [Side.Top] = 0, [Side.Bottom] = 0, [Side.Left] = 0, [Side.Right] = 0,
            };
            foreach (var ep in eps)
            {
                load[SideOf(ep)]++;
            }

            // Only faces hosting more than one endpoint need splitting; the rest stay byte-identical.
            var crowded = load.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToHashSet();
            if (crowded.Count == 0)
            {
                continue;
            }

            // Pull the laterally-displaced endpoints off their crowded face (an aligned one keeps it),
            // then reassign each — strongest lateral lean first — to a vertex, resolving contention.
            var movable = new List<(int Idx, bool IsFrom)>();
            foreach (var ep in eps)
            {
                var side = SideOf(ep);
                if (crowded.Contains(side) && Math.Abs(Lateral(ep, side)) > LateralEps)
                {
                    movable.Add(ep);
                    load[side]--;
                }
            }

            foreach (var ep in movable
                         .OrderByDescending(x => Math.Abs(Lateral(x, SideOf(x))))
                         .ThenBy(x => model.Edges[x.Idx].Id, StringComparer.Ordinal))
            {
                var origSide = SideOf(ep);
                var lateral = Lateral(ep, origSide);
                var vertical = origSide is Side.Top or Side.Bottom;
                var lowAlt = vertical ? Side.Left : Side.Top;   // far endpoint leans negative
                var highAlt = vertical ? Side.Right : Side.Bottom; // ...leans positive
                var desired = lateral < 0 ? lowAlt : highAlt;

                // Take the desired vertex when it is free; otherwise the least-loaded, preferring the
                // desired then the original face so a return edge settles back onto the vertex it left.
                var chosen = load[desired] == 0
                    ? desired
                    : new[] { desired, origSide, lowAlt, highAlt, Side.Top, Side.Bottom, Side.Left, Side.Right }
                        .OrderBy(s => load[s]).First();

                load[chosen]++;
                prep[ep.Idx] = ep.IsFrom
                    ? prep[ep.Idx]! with { FromSide = chosen }
                    : prep[ep.Idx]! with { ToSide = chosen };
            }
        }
    }

    /// <summary>
    /// Spread anchors of edges sharing a node face along it, ordered by their far
    /// endpoints — a port of <c>svg.ts:anchorShifts</c>. Keeps fan-in/out lines
    /// from stacking and A↔B pairs parallel. Also reports which anchors landed on a
    /// shared (fanned) face, so the router knows not to slide them apart again, and
    /// assigns each a turn lane so the fan's runs never share a channel.
    /// </summary>
    private static (AnchorPlan From, AnchorPlan To)[] AnchorShifts(
        IReadOnlyList<EdgeModel> edges, EdgePrep?[] prep, HashSet<string> pointNodes)
    {
        var shifts = new (AnchorPlan From, AnchorPlan To)[edges.Count];
        var groups = new Dictionary<(string Node, Side Side), List<(int Idx, bool IsFrom)>>();
        void Add(string nodeId, Side side, int idx, bool isFrom)
        {
            var key = (nodeId, side);
            if (!groups.TryGetValue(key, out var g))
            {
                groups[key] = g = new();
            }

            g.Add((idx, isFrom));
        }
        for (var i = 0; i < edges.Count; i++)
        {
            var e = edges[i];
            if (e.From == e.To)
            {
                continue;
            }

            var p = prep[i];
            if (p is null)
            {
                continue;
            }

            Add(e.From, p.FromSide, i, true);
            Add(e.To, p.ToSide, i, false);
        }

        Rect FaceRect((int Idx, bool IsFrom) r) => r.IsFrom ? prep[r.Idx]!.From : prep[r.Idx]!.To;
        Rect FarRect((int Idx, bool IsFrom) r) => r.IsFrom ? prep[r.Idx]!.To : prep[r.Idx]!.From;

        foreach (var (key, refs) in groups)
        {
            if (refs.Count < 2 || pointNodes.Contains(key.Node))
            {
                continue;
            }

            var side = key.Side;
            var rect = FaceRect(refs[0]);
            var alongY = side is Side.Left or Side.Right;
            var faceLen = alongY ? rect.H : rect.W;
            var faceCenter = alongY ? rect.Y + rect.H / 2 : rect.X + rect.W / 2;
            var step = Math.Min(20, faceLen * 0.7 / (refs.Count - 1));
            double FarCenter((int Idx, bool IsFrom) r)
            {
                var other = FarRect(r);
                return alongY ? other.Y + other.H / 2 : other.X + other.W / 2;
            }

            // Order by far endpoint; ties (same far node) by edge id — direction-stable.
            var sorted = refs.OrderBy(x => x, Comparer<(int Idx, bool IsFrom)>.Create((r1, r2) =>
            {
                var d = FarCenter(r1) - FarCenter(r2);
                if (Math.Abs(d) > 0.5)
                {
                    return d < 0 ? -1 : 1;
                }

                return string.CompareOrdinal(edges[r1.Idx].Id, edges[r2.Idx].Id) < 0 ? -1 : 1;
            })).ToList();

            const double AlignEps = 4;
            var half = faceLen * 0.7 / 2;
            var @base = (sorted.Count - 1) / 2.0;
            int alignIdx = -1, alignCount = 0;
            var bestDist = AlignEps;
            for (var i = 0; i < sorted.Count; i++)
            {
                var d = Math.Abs(FarCenter(sorted[i]) - faceCenter);
                if (d > AlignEps)
                {
                    continue;
                }

                alignCount++;
                if (d <= bestDist) { bestDist = d; alignIdx = i; }
            }
            if (alignCount == 1 && alignIdx >= 0
                && -alignIdx * step >= -half - 0.01
                && (sorted.Count - 1 - alignIdx) * step <= half + 0.01)
            {
                @base = alignIdx;
            }

            // Turn lanes, ranked by bend magnitude: the edge whose far endpoint sits furthest off
            // this face's centre has the longest run to make, so it turns first (lane 0, nearest
            // this node) and tucks that run behind its siblings' shorter turns. Ties by edge id.
            // Ranking by |shift| instead would hand a symmetric pair one shared channel — fine when
            // they bend apart, a single merged wire when they both bend the same way.
            var byBend = sorted
                .OrderByDescending(r => Math.Abs(FarCenter(r) - faceCenter))
                .ThenBy(r => edges[r.Idx].Id, StringComparer.Ordinal)
                .ToList();
            var laneOf = new Dictionary<(int, bool), Lane>();
            for (var i = 0; i < byBend.Count; i++)
            {
                laneOf[byBend[i]] = new Lane(i, byBend.Count);
            }

            for (var i = 0; i < sorted.Count; i++)
            {
                var plan = new AnchorPlan((i - @base) * step, Fanned: true, laneOf[sorted[i]]);
                if (sorted[i].IsFrom)
                {
                    shifts[sorted[i].Idx].From = plan;
                }
                else
                {
                    shifts[sorted[i].Idx].To = plan;
                }
            }
        }
        return shifts;
    }
}